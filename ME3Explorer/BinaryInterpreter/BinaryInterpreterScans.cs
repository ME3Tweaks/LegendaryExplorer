using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using Gammtek.Conduit.Extensions.IO;
using Gibbed.IO;
using ME3Explorer.Packages;
using ME3Explorer.Soundplorer;
using ME3Explorer.Unreal;
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

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Platform: {(EShaderPlatform)bin.ReadByte()}") { Length = 1 });

                int mapCount = Pcc.Game == MEGame.ME3 ? 2 : 1;
                for (; mapCount > 0; mapCount--)
                {
                    int vertexMapCount = bin.ReadInt32();
                    var mappingNode = new BinInterpTreeItem(bin.Position - 4, $"Name Mapping {mapCount}, {vertexMapCount} items");
                    subnodes.Add(mappingNode);

                    for (int i = 0; i < vertexMapCount; i++)
                    {
                        NameReference shaderName = bin.ReadNameReference(Pcc);
                        int shaderCRC = bin.ReadInt32();
                        mappingNode.Items.Add(new BinInterpTreeItem(bin.Position - 12, $"CRC:{shaderCRC:X8} {shaderName.InstancedString}") { Length = 12 });
                    }
                }

                int embeddedShaderFileCount = bin.ReadInt32();
                var embeddedShaderCount = new BinInterpTreeItem(bin.Position - 4, $"Embedded Shader File Count: {embeddedShaderFileCount}");
                subnodes.Add(embeddedShaderCount);
                for (int i = 0; i < embeddedShaderFileCount; i++)
                {
                    NameReference shaderName = bin.ReadNameReference(Pcc);
                    var shaderNode = new BinInterpTreeItem(bin.Position - 8, $"Shader {i} {shaderName.InstancedString}");

                    shaderNode.Items.Add(new BinInterpTreeItem(bin.Position - 8, $"Shader Name: {shaderName.InstancedString}") { Length = 8 });
                    shaderNode.Items.Add(new BinInterpTreeItem(bin.Position, $"Shader GUID {bin.ReadValueGuid()}") { Length = 16 });

                    int shaderEndOffset = bin.ReadInt32();
                    shaderNode.Items.Add(new BinInterpTreeItem(bin.Position - 4, $"Shader End Offset: {shaderEndOffset}") { Length = 4 });


                    shaderNode.Items.Add(new BinInterpTreeItem(bin.Position, $"Platform: {(EShaderPlatform)bin.ReadByte()}") { Length = 1 });
                    shaderNode.Items.Add(new BinInterpTreeItem(bin.Position, $"Frequency: {(EShaderFrequency)bin.ReadByte()}") { Length = 1 });

                    int shaderSize = bin.ReadInt32();
                    shaderNode.Items.Add(new BinInterpTreeItem(bin.Position - 4, $"Shader File Size: {shaderSize}") { Length = 4 });

                    shaderNode.Items.Add(new BinInterpTreeItem(bin.Position, "Shader File") { Length = shaderSize });
                    bin.Skip(shaderSize);

                    shaderNode.Items.Add(new BinInterpTreeItem(bin.Position, $"ParameterMap CRC: {bin.ReadInt32()}") { Length = 4 });

                    shaderNode.Items.Add(new BinInterpTreeItem(bin.Position, $"Shader End GUID: {bin.ReadValueGuid()}") { Length = 16 });

                    shaderNode.Items.Add(new BinInterpTreeItem(bin.Position, $"Shader Name: {bin.ReadNameReference(Pcc)}") { Length = 8 });

                    shaderNode.Items.Add(new BinInterpTreeItem(bin.Position, $"Number of Instructions: {bin.ReadInt32()}") { Length = 4 });

                    embeddedShaderCount.Items.Add(shaderNode);

                    bin.JumpTo(shaderEndOffset - dataOffset);
                }

                int vertexFactoryMapCount = bin.ReadInt32();
                var factoryMapNode = new BinInterpTreeItem(bin.Position - 4, $"Vertex Factory Name Mapping, {vertexFactoryMapCount} items");
                subnodes.Add(factoryMapNode);

                for (int i = 0; i < vertexFactoryMapCount; i++)
                {
                    NameReference shaderName = bin.ReadNameReference(Pcc);
                    int shaderCRC = bin.ReadInt32();
                    factoryMapNode.Items.Add(new BinInterpTreeItem(bin.Position - 12, $"{shaderCRC:X8} {shaderName.InstancedString}") { Length = 12 });
                }

                int materialShaderMapcount = bin.ReadInt32();
                var materialShaderMaps = new BinInterpTreeItem(bin.Position - 4, $"Material Shader Maps, {materialShaderMapcount} items");
                subnodes.Add(materialShaderMaps);
                for (int i = 0; i < materialShaderMapcount; i++)
                {
                    var nodes = new List<ITreeItem>();
                    materialShaderMaps.Items.Add(new BinInterpTreeItem(bin.Position, $"Material Shader Map {i}") { Items = nodes });
                    nodes.AddRange(ReadFStaticParameterSetStream(bin));

                    if (Pcc.Game == MEGame.ME3)
                    {
                        nodes.Add(new BinInterpTreeItem(bin.Position, $"Unreal Version {bin.ReadInt32()}") { Length = 4 });
                        nodes.Add(new BinInterpTreeItem(bin.Position, $"Licensee Version {bin.ReadInt32()}") { Length = 4 });
                    }

                    int shaderMapEndOffset = bin.ReadInt32();
                    nodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Material Shader Map end offset {shaderMapEndOffset}") { Length = 4 });

                    int unkCount = bin.ReadInt32();
                    var unkNodes = new List<ITreeItem>();
                    nodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Unknown Count {unkCount}") { Length = 4, Items = unkNodes });
                    for (int j = 0; j < unkCount; j++)
                    {
                        unkNodes.Add(new BinInterpTreeItem(bin.Position, $"Shader Name {bin.ReadNameReference(Pcc).InstancedString}") { Length = 8 });
                        unkNodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown bytes. GUID? {bin.ReadValueGuid()}") { Length = 16 });
                        unkNodes.Add(new BinInterpTreeItem(bin.Position, $"None? {bin.ReadNameReference(Pcc).InstancedString}") { Length = 8 });
                    }

                    int meshShaderMapsCount = bin.ReadInt32();
                    var meshShaderMaps = new BinInterpTreeItem(bin.Position - 4, $"Mesh Shader Maps, {meshShaderMapsCount} items") { Length = 4 };
                    nodes.Add(meshShaderMaps);
                    for (int j = 0; j < meshShaderMapsCount; j++)
                    {
                        var nodes2 = new List<ITreeItem>();
                        meshShaderMaps.Items.Add(new BinInterpTreeItem(bin.Position, $"Mesh Shader Map {j}") { Items = nodes2 });

                        int shaderCount = bin.ReadInt32();
                        var shaders = new BinInterpTreeItem(bin.Position - 4, $"Shaders, {shaderCount} items") { Length = 4 };
                        nodes2.Add(shaders);
                        for (int k = 0; k < shaderCount; k++)
                        {
                            var nodes3 = new List<ITreeItem>();
                            shaders.Items.Add(new BinInterpTreeItem(bin.Position, $"Shader {k}") { Items = nodes3 });

                            nodes3.Add(new BinInterpTreeItem(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc)}") { Length = 8 });
                            nodes3.Add(new BinInterpTreeItem(bin.Position, $"GUID: {bin.ReadValueGuid()}") { Length = 16 });
                            nodes3.Add(new BinInterpTreeItem(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc)}") { Length = 8 });
                        }
                        nodes2.Add(new BinInterpTreeItem(bin.Position, $"Vertex Factory Type: {bin.ReadNameReference(Pcc)}") { Length = 8 });
                    }

                    nodes.Add(new BinInterpTreeItem(bin.Position, $"MaterialId: {bin.ReadValueGuid()}") { Length = 16 });

                    nodes.Add(MakeStringNode(bin, "Friendly Name"));

                    nodes.AddRange(ReadFStaticParameterSetStream(bin));

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
                            nodes.Add(new BinInterpTreeItem(bin.Position - 4, $"{uniformExpressionArrayName}, {expressionCount} expressions")
                            {
                                Items = ReadList(expressionCount, x => ReadMaterialUniformExpression(bin))
                            });
                        }
                        nodes.Add(new BinInterpTreeItem(bin.Position, $"Platform: {(EShaderPlatform)bin.ReadInt32()}") { Length = 4 });
                    }

                    bin.JumpTo(shaderMapEndOffset - dataOffset);
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private BinInterpTreeItem MakeStringNode(MemoryStream bin, string nodeName)
        {
            long pos = bin.Position;
            int friendlyNameLen = bin.ReadInt32();
            string str;
            if (Pcc.Game == MEGame.ME3)
            {
                friendlyNameLen *= -2;
                str = bin.ReadStringUnicodeNull(friendlyNameLen);
            }
            else
            {
                str = bin.ReadStringASCIINull(friendlyNameLen);
            }
            return new BinInterpTreeItem(pos, $"{nodeName}: {str}") {Length = friendlyNameLen + 4};
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

        private BinInterpTreeItem ReadMaterialUniformExpression(MemoryStream bin, string prefix = "")
        {
            NameReference expressionType = bin.ReadNameReference(Pcc);
            var node = new BinInterpTreeItem(bin.Position - 8, $"{prefix}{(string.IsNullOrEmpty(prefix) ? "" : ": ")}{expressionType.InstancedString}");

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
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"NumComponentsA: {bin.ReadUInt32()}"));
                    break;
                case "FMaterialUniformExpressionClamp":
                    node.Items.Add(ReadMaterialUniformExpression(bin, "Input"));
                    node.Items.Add(ReadMaterialUniformExpression(bin, "Min"));
                    node.Items.Add(ReadMaterialUniformExpression(bin, "Max"));
                    break;
                case "FMaterialUniformExpressionConstant":
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"R: {bin.ReadSingle()}"));
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"G: {bin.ReadSingle()}"));
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"B: {bin.ReadSingle()}"));
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"A: {bin.ReadSingle()}"));
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"ValueType: {bin.ReadByte()}"));
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
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"Op: {(EFoldedMathOperation)bin.ReadByte()}"));
                    break;
                case "FMaterialUniformExpressionRealTime":
                    //intentionally left blank. outputs current real-time, has no parameters
                    break;
                case "FMaterialUniformExpressionScalarParameter":
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"ParameterName: {bin.ReadNameReference(Pcc).InstancedString}"));
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"DefaultValue: {bin.ReadSingle()}"));
                    break;
                case "FMaterialUniformExpressionSine":
                    node.Items.Add(ReadMaterialUniformExpression(bin, "X"));
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"bIsCosine: {bin.ReadBooleanInt()}"));
                    break;
                case "FMaterialUniformExpressionTexture":
                case "FMaterialUniformExpressionFlipBookTextureParameter":
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"TextureIndex: {bin.ReadUInt32()}"));
                    break;
                case "FMaterialUniformExpressionTextureParameter":
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"ParameterName: {bin.ReadNameReference(Pcc).InstancedString}"));
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"TextureIndex: {bin.ReadUInt32()}"));
                    break;
                case "FMaterialUniformExpressionTime":
                    //intentionally left blank. outputs current scene time, has no parameters
                    break;
                case "FMaterialUniformExpressionVectorParameter":
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"ParameterName: {bin.ReadNameReference(Pcc).InstancedString}"));
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"Default R: {bin.ReadSingle()}"));
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"Default G: {bin.ReadSingle()}"));
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"Default B: {bin.ReadSingle()}"));
                    node.Items.Add(new BinInterpTreeItem(bin.Position, $"Default A: {bin.ReadSingle()}"));
                    break;
                default:
                    throw new ArgumentException(expressionType.InstancedString);
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

        private static BinInterpTreeItem MakeGuidNode(byte[] data, ref int binarystart, string nodeName)
        {
            byte[] shaderStartGUIDOrSomething = new byte[16];
            Buffer.BlockCopy(data, binarystart, shaderStartGUIDOrSomething, 0, 16);
            Guid g = new Guid(shaderStartGUIDOrSomething);
            var node = new BinInterpTreeItem
            {
                Header = $"0x{binarystart:X8} {nodeName}: {g}",
                Name = "_" + binarystart,
                Tag = NodeType.Unknown,
                Length = 16
            };
            binarystart += 16;
            return node;
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
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"LODData count: {lodDataCount}"));
                subnodes.AddRange(ReadList(lodDataCount, i => new BinInterpTreeItem(bin.Position, $"LODData {i}")
                {
                    Items =
                    {
                        new BinInterpTreeItem(bin.Position, $"ShadowMaps ({bin.ReadInt32()})")
                        {
                            Items = ReadList(bin.Skip(-4).ReadInt32(), j => new BinInterpTreeItem(bin.Position, $"{j}: {entryRefString(bin)}"))
                        },
                        new BinInterpTreeItem(bin.Position, $"ShadowVertexBuffers ({bin.ReadInt32()})")
                        {
                            Items = ReadList(bin.Skip(-4).ReadInt32(), j => new BinInterpTreeItem(bin.Position, $"{j}: {entryRefString(bin)}"))
                        },
                        MakeLightMapNode(bin),
                        InitializerHelper.ConditionalAdd(Pcc.Game == MEGame.ME3, () => new List<ITreeItem>
                        {
                            new BinInterpTreeItem(bin.Position, $"bLoadVertexColorData ({bLoadVertexColorData = bin.ReadBoolByte()})"),
                            InitializerHelper.ConditionalAdd(bLoadVertexColorData, () => new ITreeItem[]
                            {
                                new BinInterpTreeItem(bin.Position, "OverrideVertexColors ")
                                {
                                    Items =
                                    {
                                        new BinInterpTreeItem(bin.Position, $"Stride: {bin.ReadUInt32()}"),
                                        new BinInterpTreeItem(bin.Position, $"NumVertices: {numVertices = bin.ReadUInt32()}"),
                                        InitializerHelper.ConditionalAdd(numVertices > 0, () => new ITreeItem[]
                                        {
                                            new BinInterpTreeItem(bin.Position, $"FColor size: {bin.ReadInt32()}"),
                                            new BinInterpTreeItem(bin.Position, $"VertexData ({bin.ReadInt32()})")
                                            {
                                                Items = ReadList(bin.Skip(-4).ReadInt32(), j => new BinInterpTreeItem(bin.Position, $"{j}")
                                                {
                                                    Items =
                                                    {
                                                        new BinInterpTreeItem(bin.Position, $"B: {bin.ReadByte()}"),
                                                        new BinInterpTreeItem(bin.Position, $"G: {bin.ReadByte()}"),
                                                        new BinInterpTreeItem(bin.Position, $"R: {bin.ReadByte()}"),
                                                        new BinInterpTreeItem(bin.Position, $"A: {bin.ReadByte()}"),
                                                    }
                                                })
                                            },
                                        }),
                                    }
                                }
                            })
                        })
                    }
                }));

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        enum ELightMapType
        {
            LMT_None,
            LMT_1D,
            LMT_2D,
            LMT_3, //speculative name. No idea what the ones after LMT_2D are actually called 
            LMT_4,
            LMT_5,
            LMT_6
        }

        private BinInterpTreeItem MakeLightMapNode(MemoryStream bin)
        {
            ELightMapType lightMapType;
            int bulkSerializeElementCount;
            int bulkSerializeDataSize;
            return new BinInterpTreeItem(bin.Position, "LightMap ")
            {
                Items =
                {
                    new BinInterpTreeItem(bin.Position, $"LightMapType: {lightMapType = (ELightMapType)bin.ReadInt32()}"),
                    InitializerHelper.ConditionalAdd(lightMapType != ELightMapType.LMT_None, () => new List<ITreeItem>
                    {
                        new BinInterpTreeItem(bin.Position, $"LightGuids ({bin.ReadInt32()})")
                        {
                            Items = ReadList(bin.Skip(-4).ReadInt32(), j => new BinInterpTreeItem(bin.Position, $"{j}: {bin.ReadGuid()}"))
                        },
                        InitializerHelper.ConditionalAdd(lightMapType == ELightMapType.LMT_1D, () => new ITreeItem[]
                        {
                            new BinInterpTreeItem(bin.Position, $"Owner: {entryRefString(bin)}"),
                            new BinInterpTreeItem(bin.Position, $"BulkDataFlags: {bin.ReadUInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"ElementCount: {bulkSerializeElementCount = bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"BulkDataSizeOnDisk: {bulkSerializeDataSize = bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"BulkDataOffsetInFile: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"DirectionalSamples: ({bulkSerializeElementCount})")
                            {
                                Items = ReadList(bulkSerializeElementCount, j => new BinInterpTreeItem(bin.Position, $"{j}")
                                {
                                    Items = ReadList(bulkSerializeDataSize / bulkSerializeElementCount / 4, k => new BinInterpTreeItem(bin.Position,
                                                                                                                                       $"(B: {bin.ReadByte()}, G: {bin.ReadByte()}, R: {bin.ReadByte()}, A: {bin.ReadByte()})"))
                                })
                            },
                            MakeVectorNode(bin, "ScaleVector 1"),
                            MakeVectorNode(bin, "ScaleVector 2"),
                            MakeVectorNode(bin, "ScaleVector 3"),
                            Pcc.Game != MEGame.ME3 ? MakeVectorNode(bin, "ScaleVector 4") : null,
                            new BinInterpTreeItem(bin.Position, $"BulkDataFlags: {bin.ReadUInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"ElementCount: {bulkSerializeElementCount = bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"BulkDataSizeOnDisk: {bulkSerializeDataSize = bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"BulkDataOffsetInFile: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"SimpleSamples: ({bulkSerializeElementCount})")
                            {
                                Items = ReadList(bulkSerializeElementCount, j => new BinInterpTreeItem(bin.Position, $"{j}")
                                {
                                    Items = ReadList(bulkSerializeDataSize / bulkSerializeElementCount / 4, k => new BinInterpTreeItem(bin.Position,
                                                                                                                                       $"(B: {bin.ReadByte()}, G: {bin.ReadByte()}, R: {bin.ReadByte()}, A: {bin.ReadByte()})"))
                                })
                            },
                        }.NonNull()),
                        InitializerHelper.ConditionalAdd(lightMapType == ELightMapType.LMT_2D, () => new List<ITreeItem>
                        {
                            new BinInterpTreeItem(bin.Position, $"Texture 1: {entryRefString(bin)}"),
                            MakeVectorNode(bin, "ScaleVector 1"),
                            new BinInterpTreeItem(bin.Position, $"Texture 2 {entryRefString(bin)}"),
                            MakeVectorNode(bin, "ScaleVector 2"),
                            new BinInterpTreeItem(bin.Position, $"Texture 3 {entryRefString(bin)}"),
                            MakeVectorNode(bin, "ScaleVector 3"),
                            InitializerHelper.ConditionalAdd(Pcc.Game != MEGame.ME3, () => new ITreeItem[]
                            {
                                new BinInterpTreeItem(bin.Position, $"Texture 4 {entryRefString(bin)}"),
                                MakeVectorNode(bin, "ScaleVector 4"),
                            }),
                            new BinInterpTreeItem(bin.Position, $"CoordinateScale: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()})"),
                            new BinInterpTreeItem(bin.Position, $"CoordinateBias: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()})")
                        }),
                        InitializerHelper.ConditionalAdd(lightMapType == ELightMapType.LMT_3, () => new ITreeItem[]
                        {
                            new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"BulkDataFlags: {bin.ReadUInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"ElementCount: {bulkSerializeElementCount = bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"BulkDataSizeOnDisk: {bulkSerializeDataSize = bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"BulkDataOffsetInFile: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"DirectionalSamples?: ({bulkSerializeElementCount})")
                            {
                                Items = ReadList(bulkSerializeElementCount, j => new BinInterpTreeItem(bin.Position, $"{j}")
                                {
                                    Items = ReadList(bulkSerializeDataSize / bulkSerializeElementCount / 4, k => new BinInterpTreeItem(bin.Position,
                                                                                                                                       $"(B: {bin.ReadByte()}, G: {bin.ReadByte()}, R: {bin.ReadByte()}, A: {bin.ReadByte()})"))
                                })
                            },
                            MakeVectorNode(bin, "ScaleVector?"),
                            MakeVectorNode(bin, "ScaleVector?")
                        }),
                        InitializerHelper.ConditionalAdd(lightMapType == ELightMapType.LMT_4 || lightMapType == ELightMapType.LMT_6, () => new List<ITreeItem>
                        {
                            new BinInterpTreeItem(bin.Position, $"Texture 1: {entryRefString(bin)}"),
                            new InitializerHelper.InitializerCollection<ITreeItem>(ReadList(8, j => new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}"))),
                            new BinInterpTreeItem(bin.Position, $"Texture 2: {entryRefString(bin)}"),
                            new InitializerHelper.InitializerCollection<ITreeItem>(ReadList(8, j => new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}"))),
                            new BinInterpTreeItem(bin.Position, $"Texture 3: {entryRefString(bin)}"),
                            new InitializerHelper.InitializerCollection<ITreeItem>(ReadList(8, j => new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}"))),
                            new InitializerHelper.InitializerCollection<ITreeItem>(ReadList(4, j => new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}"))),
                        }),
                        InitializerHelper.ConditionalAdd(lightMapType == ELightMapType.LMT_5, () => new ITreeItem[]
                        {
                            new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"BulkDataFlags: {bin.ReadUInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"ElementCount: {bulkSerializeElementCount = bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"BulkDataSizeOnDisk: {bulkSerializeDataSize = bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"BulkDataOffsetInFile: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"SimpleSamples?: ({bulkSerializeElementCount})")
                            {
                                Items = ReadList(bulkSerializeElementCount, j => new BinInterpTreeItem(bin.Position, $"{j}")
                                {
                                    Items = ReadList(bulkSerializeDataSize / bulkSerializeElementCount / 4, k => new BinInterpTreeItem(bin.Position,
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
                subnodes.Add(new BinInterpTreeItem(bin.Position, "CachedPhysBrushData")
                {
                    IsExpanded = true,
                    Items =
                    {
                        new BinInterpTreeItem(bin.Position, $"CachedConvexElements ({cachedConvexElementsCount = bin.ReadInt32()})")
                        {
                            Items = ReadList(cachedConvexElementsCount, j =>
                            {
                                int size;
                                var item = new BinInterpTreeItem(bin.Position, $"{j}: ConvexElementData (size of byte: {bin.ReadInt32()}) (number of bytes: {size = bin.ReadInt32()})")
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
                subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
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

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Model: {entryRefString(bin)}"));
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"ZoneIndex: {bin.ReadInt32()}"));
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Elements ({count = bin.ReadInt32()})")
                {
                    Items = ReadList(count, i => new BinInterpTreeItem(bin.Position, $"{i}: FModelElement")
                    {
                        Items =
                        {
                            MakeLightMapNode(bin),
                            new BinInterpTreeItem(bin.Position, $"Component: {entryRefString(bin)}"),
                            new BinInterpTreeItem(bin.Position, $"Material: {entryRefString(bin)}"),
                            new BinInterpTreeItem(bin.Position, $"Nodes ({count = bin.ReadInt32()})")
                            {
                                Items = ReadList(count, j => new BinInterpTreeItem(bin.Position, $"{j}: {bin.ReadUInt16()}"))
                            },
                            new BinInterpTreeItem(bin.Position, $"ShadowMaps ({count = bin.ReadInt32()})")
                            {
                                Items = ReadList(count, j => new BinInterpTreeItem(bin.Position, $"{j}: {entryRefString(bin)}"))
                            },
                            new BinInterpTreeItem(bin.Position, $"IrrelevantLights ({count = bin.ReadInt32()})")
                            {
                                Items = ReadList(count, j => new BinInterpTreeItem(bin.Position, $"{j}: {bin.ReadGuid()}"))
                            }
                        }
                    })
                });
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"ComponentIndex: {bin.ReadUInt16()}"));
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Nodes ({count = bin.ReadInt32()})")
                {
                    Items = ReadList(count, i => new BinInterpTreeItem(bin.Position, $"{i}: {bin.ReadUInt16()}"))
                });

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private List<ITreeItem> StartLightComponentScan(byte[] data, int binarystart)
        {

            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(data);
                bin.JumpTo(binarystart);

                int count;
                foreach (string propName in new[]{"InclusionConvexVolumes", "ExclusionConvexVolumes"})
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"{propName} ({count = bin.ReadInt32()})")
                    {
                        Items = ReadList(count, i => new BinInterpTreeItem(bin.Position, $"{i}")
                        {
                            Items =
                            {
                                new BinInterpTreeItem(bin.Position, $"Planes ({count = bin.ReadInt32()})")
                                {
                                    Items = ReadList(count, j =>
                                                         new BinInterpTreeItem(bin.Position, $"{j}: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()}, Z: {bin.ReadSingle()}, W: {bin.ReadSingle()})"))
                                },
                                new BinInterpTreeItem(bin.Position, $"PermutedPlanes ({count = bin.ReadInt32()})")
                                {
                                    Items = ReadList(count, j =>
                                                         new BinInterpTreeItem(bin.Position, $"{j}: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()}, Z: {bin.ReadSingle()}, W: {bin.ReadSingle()})"))
                                }
                            }
                        })
                    });
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private List<ITreeItem> StartDominantLightScan(byte[] data)
        {

            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(data);


                if (Pcc.Game == MEGame.ME3)
                {
                    int count;
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"DominantLightShadowMap ({count = bin.ReadInt32()})")
                    {
                        Items = ReadList(count, i => new BinInterpTreeItem(bin.Position, $"{i}: {bin.ReadUInt16()}"))
                    });
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
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
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"AnimationMap? ({count = bin.ReadInt32()})")
                {
                    Items = ReadList(count, i => new BinInterpTreeItem(bin.Position, $"{bin.ReadNameReference(Pcc)}: {entryRefString(bin)}"))
                });

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
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
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"float size ({bin.ReadInt32()})"));
                }

                int sampleCount = bin.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Samples ({sampleCount})")
                {
                    Items = ReadList(sampleCount, i => new BinInterpTreeItem(bin.Position, $"{i}: {bin.ReadSingle()}"))
                });
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"LightGuid ({bin.ReadGuid()})"));
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
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
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Count: {polysCount}"));
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Max: {bin.ReadInt32()}"));
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Owner (self): {entryRefString(bin)}"));
                if (polysCount > 0)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Elements ({polysCount})")
                    {
                        Items = ReadList(polysCount, i => new BinInterpTreeItem(bin.Position, $"{i}")
                        {
                            Items = new List<ITreeItem>
                            {
                                MakeVectorNode(bin, "Base"),
                                MakeVectorNode(bin, "Normal"),
                                MakeVectorNode(bin, "TextureU"),
                                MakeVectorNode(bin, "TextureV"),
                                new BinInterpTreeItem(bin.Position, $"Vertices ({bin.ReadInt32()})")
                                {
                                    Items = ReadList(bin.Skip(-4).ReadInt32(), j =>
                                                         new BinInterpTreeItem(bin.Position, $"{j}: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()}, Z: {bin.ReadSingle()})"))
                                },
                                new BinInterpTreeItem(bin.Position, $"PolyFlags: {bin.ReadInt32()}"),
                                new BinInterpTreeItem(bin.Position, $"Actor: {entryRefString(bin)}"),
                                new BinInterpTreeItem(bin.Position, $"ItemName: {bin.ReadNameReference(Pcc)}"),
                                new BinInterpTreeItem(bin.Position, $"Material: {entryRefString(bin)}"),
                                new BinInterpTreeItem(bin.Position, $"iLink: {bin.ReadInt32()}"),
                                new BinInterpTreeItem(bin.Position, $"iBrushPoly: {bin.ReadInt32()}"),
                                new BinInterpTreeItem(bin.Position, $"ShadowMapScale: {bin.ReadSingle()}"),
                                new BinInterpTreeItem(bin.Position, $"LightingChannels: {bin.ReadInt32()}"),
                                InitializerHelper.ConditionalAdd(Pcc.Game == MEGame.ME3, () => new ITreeItem[]
                                {
                                    new BinInterpTreeItem(bin.Position, $"bUseTwoSidedLighting: {bin.ReadBoolInt()}"),
                                    new BinInterpTreeItem(bin.Position, $"bShadowIndirectOnly: {bin.ReadBoolInt()}"),
                                    new BinInterpTreeItem(bin.Position, $"FullyOccludedSamplesFraction: {bin.ReadSingle()}"),
                                    new BinInterpTreeItem(bin.Position, $"bUseEmissiveForStaticLighting: {bin.ReadBoolInt()}"),
                                    new BinInterpTreeItem(bin.Position, $"EmissiveLightFalloffExponent: {bin.ReadSingle()}"),
                                    new BinInterpTreeItem(bin.Position, $"EmissiveLightExplicitInfluenceRadius: {bin.ReadSingle()}"),
                                    new BinInterpTreeItem(bin.Position, $"EmissiveBoost: {bin.ReadSingle()}"),
                                    new BinInterpTreeItem(bin.Position, $"DiffuseBoost: {bin.ReadSingle()}"),
                                    new BinInterpTreeItem(bin.Position, $"SpecularBoost: {bin.ReadSingle()}"),
                                    new BinInterpTreeItem(bin.Position, $"RulesetVariation: {bin.ReadNameReference(Pcc)}")
                                }),
                            }
                        })
                    });
                }


                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
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

                subnodes.Add(new BinInterpTreeItem(bin.Position, "Bounds")
                {
                    Items = new List<ITreeItem>
                    {
                        MakeVectorNode(bin, "Origin"),
                        MakeVectorNode(bin, "BoxExtent"),
                        new BinInterpTreeItem(bin.Position, $"SphereRadius: {bin.ReadSingle()}")
                    }
                });

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"FVector Size: {bin.ReadInt32()}"));
                int vectorsCount = bin.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Vectors ({vectorsCount})")
                {
                    Items = ReadList(vectorsCount, i => new BinInterpTreeItem(bin.Position, $"{i}: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()}, Z: {bin.ReadSingle()})"))
                });

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"FVector Size: {bin.ReadInt32()}"));
                int pointsCount = bin.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Points ({pointsCount})")
                {
                    Items = ReadList(pointsCount, i => new BinInterpTreeItem(bin.Position, $"{i}: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()}, Z: {bin.ReadSingle()})"))
                });

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"FBspNode Size: {bin.ReadInt32()}"));
                int nodesCount = bin.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Nodes ({nodesCount})")
                {
                    Items = ReadList(nodesCount, i => new BinInterpTreeItem(bin.Position, $"{i}")
                    {
                        Items = new List<ITreeItem>
                        {
                            new BinInterpTreeItem(bin.Position, $"Plane: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()}, Z: {bin.ReadSingle()}, W: {bin.ReadSingle()})"),
                            new BinInterpTreeItem(bin.Position, $"iVertPool: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"iSurf: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"iVertexIndex: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"ComponentIndex: {bin.ReadUInt16()}"),
                            new BinInterpTreeItem(bin.Position, $"ComponentNodeIndex: {bin.ReadUInt16()}"),
                            new BinInterpTreeItem(bin.Position, $"ComponentElementIndex: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"iBack: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"iFront: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"iPlane: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"iCollisionBound: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"iZone[0]: {bin.ReadByte()}"),
                            new BinInterpTreeItem(bin.Position, $"iZone[1]: {bin.ReadByte()}"),
                            new BinInterpTreeItem(bin.Position, $"NumVertices: {bin.ReadByte()}"),
                            new BinInterpTreeItem(bin.Position, $"NodeFlags: {bin.ReadByte()}"),
                            new BinInterpTreeItem(bin.Position, $"iLeaf[0]: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"iLeaf[1]: {bin.ReadInt32()}")
                        }
                    })
                });

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Owner (self): {entryRefString(bin)}"));
                int surfsCount = bin.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Surfaces ({surfsCount})")
                {
                    Items = ReadList(surfsCount, i => new BinInterpTreeItem(bin.Position, $"{i}")
                    {
                        Items = new List<ITreeItem>
                        {
                            new BinInterpTreeItem(bin.Position, $"Material: {entryRefString(bin)}"),
                            new BinInterpTreeItem(bin.Position, $"PolyFlags: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"pBase: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"vNormal: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"vTextureU: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"vTextureV: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"iBrushPoly: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"Actor: {entryRefString(bin)}"),
                            new BinInterpTreeItem(bin.Position, $"Plane: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()}, Z: {bin.ReadSingle()}, W: {bin.ReadSingle()})"),
                            new BinInterpTreeItem(bin.Position, $"ShadowMapScale: {bin.ReadSingle()}"),
                            new BinInterpTreeItem(bin.Position, $"LightingChannels(Bitfield): {bin.ReadInt32()}"),
                            Pcc.Game == MEGame.ME3 ? new BinInterpTreeItem(bin.Position, $"iBrushPoly: {bin.ReadInt32()}") : null,
                        }.NonNull().ToList()
                    })
                });

                int fVertSize = bin.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"FVert Size: {fVertSize}"));
                int vertsCount = bin.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Verts ({vertsCount})")
                {
                    Items = ReadList(vertsCount, i => new BinInterpTreeItem(bin.Position, $"{i}")
                    {
                        Items = new List<ITreeItem>
                        {
                            new BinInterpTreeItem(bin.Position, $"pVertex: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"iSide: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"ShadowTexCoord: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()})"),
                            fVertSize == 24 ? new BinInterpTreeItem(bin.Position, $"BackfaceShadowTexCoord: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()})") : null
                        }.NonNull().ToList()
                    })
                });

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"NumSharedSides: {bin.ReadInt32()}"));
                int numZones = bin.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"NumZones: {numZones}")
                {
                    Items = ReadList(numZones, i => new BinInterpTreeItem(bin.Position, $"Zone {i}")
                    {
                        Items = new List<ITreeItem>
                        {
                            new BinInterpTreeItem(bin.Position, $"ZoneActor: {entryRefString(bin)}"),
                            new BinInterpTreeItem(bin.Position, $"LastRenderTime: {bin.ReadSingle()}"),
                            new BinInterpTreeItem(bin.Position, $"Connectivity: {Convert.ToString(bin.ReadInt64(), 2).PadLeft(64, '0')}"),
                            new BinInterpTreeItem(bin.Position, $"Visibility: {Convert.ToString(bin.ReadInt64(), 2).PadLeft(64, '0')}"),
                        }
                    })
                });

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Polys: {entryRefString(bin)}"));

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"integer Size: {bin.ReadInt32()}"));
                int leafHullsCount = bin.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"LeafHulls ({leafHullsCount})")
                {
                    Items = ReadList(leafHullsCount, i => new BinInterpTreeItem(bin.Position, $"{i}: {bin.ReadInt32()}"))
                });

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"FLeaf Size: {bin.ReadInt32()}"));
                int leavesCount = bin.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Leaves ({leavesCount})")
                {
                    Items = ReadList(leavesCount, i => new BinInterpTreeItem(bin.Position, $"{i}: iZone: {bin.ReadInt32()}"))
                });


                subnodes.Add(new BinInterpTreeItem(bin.Position, $"RootOutside: {bin.ReadBoolInt()}"));
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Linked: {bin.ReadBoolInt()}"));

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"integer Size: {bin.ReadInt32()}"));
                int portalNodesCount = bin.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"PortalNodes ({portalNodesCount})")
                {
                    Items = ReadList(portalNodesCount, i => new BinInterpTreeItem(bin.Position, $"{i}: {bin.ReadInt32()}"))
                });

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"FMeshEdge Size: {bin.ReadInt32()}"));
                int legacyedgesCount = bin.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"ShadowVolume? ({legacyedgesCount})")
                {
                    Items = ReadList(legacyedgesCount, i => new BinInterpTreeItem(bin.Position, $"MeshEdge {i}")
                    {
                        Items = new List<ITreeItem>
                        {
                            new BinInterpTreeItem(bin.Position, $"Vertices[0]: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"Vertices[1]: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"Faces[0]: {bin.ReadInt32()}"),
                            new BinInterpTreeItem(bin.Position, $"Faces[1]: {bin.ReadInt32()}")
                        }
                    })
                });

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"NumVertices: {bin.ReadUInt32()}"));

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"FModelVertex Size: {bin.ReadInt32()}"));
                int verticesCount = bin.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"VertexBuffer Vertices({verticesCount})")
                {
                    Items = ReadList(verticesCount, i => new BinInterpTreeItem(bin.Position, $"{i}")
                    {
                        Items = new List<ITreeItem>
                        {
                            MakeVectorNode(bin, "Position"),
                            new BinInterpTreeItem(bin.Position, $"TangentX: (X: {bin.ReadByte()}, Y: {bin.ReadByte()}, Z: {bin.ReadByte()}, W: {bin.ReadByte()})"),
                            new BinInterpTreeItem(bin.Position, $"TangentZ: (X: {bin.ReadByte()}, Y: {bin.ReadByte()}, Z: {bin.ReadByte()}, W: {bin.ReadByte()})"),
                            new BinInterpTreeItem(bin.Position, $"TexCoord: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()})"),
                            new BinInterpTreeItem(bin.Position, $"ShadowTexCoord: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()})")
                        }
                    })
                });

                if (Pcc.Game == MEGame.ME3)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"LightingGuid: {bin.ReadValueGuid()}") { Length = 16 });

                    int lightmassSettingsCount = bin.ReadInt32();
                    subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"LightmassSettings ({lightmassSettingsCount})")
                    {
                        Items = ReadList(lightmassSettingsCount, i => new BinInterpTreeItem(bin.Position, $"{i}")
                        {
                            Items = new List<ITreeItem>
                            {
                                new BinInterpTreeItem(bin.Position, $"bUseTwoSidedLighting: {bin.ReadBoolInt()}"),
                                new BinInterpTreeItem(bin.Position, $"bShadowIndirectOnly: {bin.ReadBoolInt()}"),
                                new BinInterpTreeItem(bin.Position, $"FullyOccludedSamplesFraction: {bin.ReadSingle()}"),
                                new BinInterpTreeItem(bin.Position, $"bUseEmissiveForStaticLighting: {bin.ReadBoolInt()}"),
                                new BinInterpTreeItem(bin.Position, $"EmissiveLightFalloffExponent: {bin.ReadSingle()}"),
                                new BinInterpTreeItem(bin.Position, $"EmissiveLightExplicitInfluenceRadius: {bin.ReadSingle()}"),
                                new BinInterpTreeItem(bin.Position, $"EmissiveBoost: {bin.ReadSingle()}"),
                                new BinInterpTreeItem(bin.Position, $"DiffuseBoost: {bin.ReadSingle()}"),
                                new BinInterpTreeItem(bin.Position, $"SpecularBoost: {bin.ReadSingle()}")
                            }
                        })
                    });
                }

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
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

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"PersistentLevel: {entryRefString(bin)}"));
                if (Pcc.Game == MEGame.ME3)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"PersistentFaceFXAnimSet: {entryRefString(bin)}"));
                }
                subnodes.AddRange(ReadList(4, i => new BinInterpTreeItem(bin.Position, $"EditorView {i}")
                {
                    Items =
                    {
                        MakeVectorNode(bin, "CamPosition"),
                        new BinInterpTreeItem(bin.Position, $"CamRotation: (Pitch: {bin.ReadInt32()}, Yaw: {bin.ReadInt32()}, Roll: {bin.ReadInt32()})"),
                        new BinInterpTreeItem(bin.Position, $"CamOrthoZoom: {bin.ReadSingle()}")
                    }
                }));
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Null: {entryRefString(bin)}"));
                if (Pcc.Game == MEGame.ME1)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"DecalManager: {entryRefString(bin)}"));
                }

                int extraObjsCount;
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"ExtraReferencedObjects: {extraObjsCount = bin.ReadInt32()}")
                {
                    Items = ReadList(extraObjsCount, i => new BinInterpTreeItem(bin.Position, $"{entryRefString(bin)}"))
                });

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private List<ITreeItem> StartStackScan(byte[] data)
        {
            var subnodes = new List<ITreeItem>();
            int binarystart = 0;
            int importNum = BitConverter.ToInt32(data, binarystart);
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"{binarystart:X4} Class: {importNum} ({CurrentLoadedExport.FileRef.GetEntryString(importNum)})",
                Name = "_" + binarystart,
                Tag = NodeType.StructLeafObject
            });
            binarystart += 4;
            importNum = BitConverter.ToInt32(data, binarystart);
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"{binarystart:X4} Class: {importNum} ({CurrentLoadedExport.FileRef.GetEntryString(importNum)})",
                Name = "_" + binarystart,
                Tag = NodeType.StructLeafObject
            });
            binarystart += 4;
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"{binarystart:X4} Null: {BitConverter.ToInt32(data, binarystart)}",
                Name = "_" + binarystart
            });
            binarystart += 4;
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"{binarystart:X4} Null: {BitConverter.ToInt32(data, binarystart)}",
                Name = "_" + binarystart
            });
            binarystart += 4;
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"{binarystart:X4} ????: {BitConverter.ToInt32(data, binarystart)}",
                Name = "_" + binarystart
            });
            binarystart += 4;
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"{binarystart:X4} ????: {BitConverter.ToInt16(data, binarystart)}",
                Name = "_" + binarystart
            });
            binarystart += 2;
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"{binarystart:X4} Null: {BitConverter.ToInt32(data, binarystart)}",
                Name = "_" + binarystart
            });
            binarystart += 4;
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"{binarystart:X4} NetIndex: {BitConverter.ToInt32(data, binarystart)}",
                Name = "_" + binarystart,
                Tag = NodeType.StructLeafInt
            });

            return subnodes;
        }

        private List<ITreeItem> StartMetaDataScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                int offset = binarystart;

                int count = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
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
                        label = CurrentLoadedExport.FileRef.getNameEntry(nameIdx);
                        ms.ReadInt32();
                    }

                    var strLen = ms.ReadUInt32();
                    var line = Gibbed.IO.StreamHelpers.ReadString(ms, strLen, true, Encoding.ASCII);
                    if (label != null)
                    {
                        subnodes.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X6}    {label}:\n{line}\n",
                            Name = "_" + offset,
                            Tag = NodeType.None
                        });
                    }
                    else
                    {
                        subnodes.Add(new BinInterpTreeItem
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
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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
                    subnodes.Add(new BinInterpTreeItem
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
                        var languageNode = new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X4} {CurrentLoadedExport.FileRef.getNameEntry(langRef)} - {langTlkCount} entries",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafName,
                            IsExpanded = true
                        };
                        subnodes.Add(languageNode);
                        offset += 12;

                        for (int k = 0; k < langTlkCount; k++)
                        {
                            int tlkIndex = BitConverter.ToInt32(data, offset); //-1 in reader
                            languageNode.Items.Add(new BinInterpTreeItem
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
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X8} Item1: {classObjTree} (0x{classObjTree:X8})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X8} Data length: {classObjTree} (0x{classObjTree:X8})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X8} Data length: {classObjTree} (0x{classObjTree:X8})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
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
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Streaming Data Size: {numBytesOfStreamingData}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                var nextFileOffset = BitConverter.ToInt32(data, offset);
                var node = new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Next file offset: {nextFileOffset}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                };

                var clickToGotoOffset = new BinInterpTreeItem
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
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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
                var EventCountNode = new BinInterpTreeItem
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
                    var EventIDs = new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} State Transition ID: {iEventID} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    EventCountNode.Items.Add(EventIDs);

                    int EventMapInstVer = BitConverter.ToInt32(data, offset); //Event Instance Version
                    EventIDs.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Instance Version: {EventMapInstVer} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int nTransitions = BitConverter.ToInt32(data, offset); //Count of State Events
                    var TransitionsIDs = new BinInterpTreeItem
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
                            var nTransition = new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Type: {transTYPE} Transition on Bool {tPlotID}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            };
                            offset += 4;
                            TransitionsIDs.Items.Add(nTransition);

                            int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            tPlotID = BitConverter.ToInt32(data, offset);  //Plot
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Plot ID: {tPlotID} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tNewValue = BitConverter.ToInt32(data, offset);  //NewValue
                            bool bNewValue = false;
                            if (tNewValue == 1) { bNewValue = true; }
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} New Value: {tNewValue}  {bNewValue} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tUseParam = BitConverter.ToInt32(data, offset);  //Use Parameter bool
                            bool bUseParam = false;
                            if (tUseParam == 1) { bUseParam = true; }
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Use parameter: {tUseParam}  {bUseParam} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;
                        }
                        else if (transTYPE == 1) //TYPE 1 = CONSEQUENCE
                        {
                            var nTransition = new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Type: {transTYPE} Consequence",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            };
                            offset += 4;
                            TransitionsIDs.Items.Add(nTransition);

                            int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tConsequenceParam = BitConverter.ToInt32(data, offset);  //Consequence parameter
                            nTransition.Items.Add(new BinInterpTreeItem
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
                            var nTransition = new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Type: {transTYPE} transition on Float {tPlotID}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            };
                            offset += 4;
                            TransitionsIDs.Items.Add(nTransition);

                            int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            tPlotID = BitConverter.ToInt32(data, offset);  //Plot
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Plot ID: {tPlotID} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            float tNewValue = BitConverter.ToInt32(data, offset);  //NewValue
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} New Value: {tNewValue} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafFloat
                            });
                            offset += 4;

                            int tUseParam = BitConverter.ToInt32(data, offset);  //Use Parameter bool
                            bool bUseParam = false;
                            if (tUseParam == 1) { bUseParam = true; }
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Use parameter: {tUseParam}  {bUseParam} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tIncrement = BitConverter.ToInt32(data, offset);  //Increment bool
                            bool bIncrement = false;
                            if (tIncrement == 1) { bIncrement = true; }
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Increment value: {tIncrement}  {bIncrement} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;
                        }
                        else if (transTYPE == 3)  // TYPE 3 = FUNCTION
                        {
                            var nTransition = new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Type: {transTYPE} Function",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            };
                            offset += 4;
                            TransitionsIDs.Items.Add(nTransition);

                            int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int PackageName = BitConverter.ToInt32(data, offset);  //Package name
                            offset += 4;
                            int PackageIdx = BitConverter.ToInt32(data, offset);  //Package name idx
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Package Name: {CurrentLoadedExport.FileRef.getNameEntry(PackageName)}_{PackageIdx}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafName
                            });
                            offset += 4;

                            int ClassName = BitConverter.ToInt32(data, offset);  //Class name
                            offset += 4;
                            int ClassIdx = BitConverter.ToInt32(data, offset);  //Class name idx
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Class Name: {CurrentLoadedExport.FileRef.getNameEntry(ClassName)}_{ClassIdx}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafName
                            });
                            offset += 4;

                            int FunctionName = BitConverter.ToInt32(data, offset);  //Function name
                            offset += 4;
                            int FunctionIdx = BitConverter.ToInt32(data, offset);  //Function name idx
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Function Name: {CurrentLoadedExport.FileRef.getNameEntry(FunctionName)}_{FunctionIdx}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafName
                            });
                            offset += 4;


                            int Parameter = BitConverter.ToInt32(data, offset);  //Parameter
                            nTransition.Items.Add(new BinInterpTreeItem
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
                            var nTransition = new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Type: {transTYPE} transition on INT {tPlotID}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            };
                            offset += 4;
                            TransitionsIDs.Items.Add(nTransition);

                            int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            tPlotID = BitConverter.ToInt32(data, offset);  //Plot
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Plot ID: {tPlotID} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tNewValue = BitConverter.ToInt32(data, offset);  //NewValue
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} New Value: {tNewValue} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafFloat
                            });
                            offset += 4;

                            int tUseParam = BitConverter.ToInt32(data, offset);  //Use Parameter bool
                            bool bUseParam = false;
                            if (tUseParam == 1) { bUseParam = true; }
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Use parameter: {tUseParam}  {bUseParam} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tIncrement = BitConverter.ToInt32(data, offset);  //Increment bool
                            bool bIncrement = false;
                            if (tIncrement == 1) { bIncrement = true; }
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Increment value: {tIncrement}  {bIncrement} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;
                        }
                        else if (transTYPE == 5)  // TYPE 5 = LOCAL BOOL
                        {
                            var nTransition = new BinInterpTreeItem
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
                            var nTransition = new BinInterpTreeItem
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
                            var nTransition = new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Type: {transTYPE} Local Int",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            };
                            offset += 4;
                            TransitionsIDs.Items.Add(nTransition);

                            int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tObjtag = BitConverter.ToInt32(data, offset);  //Use Object tag??
                            bool bObjtag = false;
                            if (tObjtag == 1) { bObjtag = true; }
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Object Tag: {tObjtag}  {bObjtag} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int FunctionName = BitConverter.ToInt32(data, offset);  //Function name
                            offset += 4;
                            int FunctionIdx = BitConverter.ToInt32(data, offset);  //Function name
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Function Name: {CurrentLoadedExport.FileRef.getNameEntry(FunctionName)}_{FunctionIdx}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafName
                            });
                            offset += 4;

                            int TagName = BitConverter.ToInt32(data, offset);  //Object name
                            offset += 4;
                            int TagIdx = BitConverter.ToInt32(data, offset);  //Object idx
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Object Name: {CurrentLoadedExport.FileRef.getNameEntry(TagName)}_{TagIdx}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafName
                            });
                            offset += 4;

                            int tUseParam = BitConverter.ToInt32(data, offset);  //Use Parameter bool
                            bool bUseParam = false;
                            if (tUseParam == 1) { bUseParam = true; }
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Use parameter: {tUseParam}  {bUseParam} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tNewValue = BitConverter.ToInt32(data, offset);  //NewValue
                            nTransition.Items.Add(new BinInterpTreeItem
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
                            var nTransition = new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Type: {transTYPE} Substate Transition on Bool {tPlotID}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            };
                            offset += 4;
                            TransitionsIDs.Items.Add(nTransition);

                            int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            tPlotID = BitConverter.ToInt32(data, offset);  //Plot
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Plot ID: {tPlotID} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tNewValue = BitConverter.ToInt32(data, offset);  //NewState Bool
                            bool bNewValue = false;
                            if (tNewValue == 1) { bNewValue = true; }
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} New State: {tNewValue}  {bNewValue}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tUseParam = BitConverter.ToInt32(data, offset);  //Use Parameter bool
                            bool bUseParam = false;
                            if (tUseParam == 1) { bUseParam = true; }
                            nTransition.Items.Add(new BinInterpTreeItem
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
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Parent OR type: {tParentType}  {bParentType} {sParentType}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int ParentIdx = BitConverter.ToInt32(data, offset);  //Parent Bool
                            nTransition.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Parent Bool: {ParentIdx} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int sibCount = BitConverter.ToInt32(data, offset); //Sibling Substates
                            var SiblingIDs = new BinInterpTreeItem
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
                                var nSiblings = new BinInterpTreeItem
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
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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
                var QuestNode = new BinInterpTreeItem
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
                    var QuestIDs = new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Quest ID: {iQuestID} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    QuestNode.Items.Add(QuestIDs);

                    int Unknown1 = BitConverter.ToInt32(data, offset); //Unknown1
                    QuestIDs.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Unknown: {Unknown1} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int Unknown2 = BitConverter.ToInt32(data, offset); //Unknown2
                    QuestIDs.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Unknown: {Unknown2} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int gCount = BitConverter.ToInt32(data, offset); //Goal Count
                    var GoalsIDs = new BinInterpTreeItem
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
                        var nGoalIDs = new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Goal start plot/cnd: {goalStart} { startType }",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        };
                        GoalsIDs.Items.Add(nGoalIDs);

                        int iGoalInstVersion = BitConverter.ToInt32(data, offset);  //Goal Instance Version
                        nGoalIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Goal Instance Version: {iGoalInstVersion} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int gTitle = BitConverter.ToInt32(data, offset); //Goal Name
                        string gttlkLookup = GlobalFindStrRefbyID(gTitle, game, CurrentLoadedExport.FileRef as ME1Package);
                        nGoalIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Goal Name StrRef: {gTitle} { gttlkLookup }",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafObject
                        });
                        offset += 4;

                        int gDescription = BitConverter.ToInt32(data, offset); //Goal Description
                        string gdtlkLookup = GlobalFindStrRefbyID(gDescription, game, CurrentLoadedExport.FileRef as ME1Package);
                        nGoalIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Goal Description StrRef: {gDescription} { gdtlkLookup }",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafObject
                        });
                        offset += 4;

                        gConditional = BitConverter.ToInt32(data, offset); //Conditional
                        nGoalIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Conditional: {gConditional} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        gState = BitConverter.ToInt32(data, offset); //State
                        nGoalIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Bool State: {gState} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;
                    }

                    int tCount = BitConverter.ToInt32(data, offset); //Task Count
                    var TaskIDs = new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Tasks Count: {tCount} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    QuestIDs.Items.Add(TaskIDs);

                    for (int t = 0; t < tCount; t++)  //TASKS
                    {

                        var nTaskIDs = new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Task: {t}",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        };
                        TaskIDs.Items.Add(nTaskIDs);

                        int iTaskInstVersion = BitConverter.ToInt32(data, offset);  //Task Instance Version
                        nTaskIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Task Instance Version: {iTaskInstVersion} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int tFinish = BitConverter.ToInt32(data, offset); //Primary Codex
                        bool bFinish = false;
                        if (tFinish == 1) { bFinish = true; }
                        nTaskIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Task Finishes Quest: {tFinish}  { bFinish }",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafObject
                        });
                        offset += 4;

                        int tTitle = BitConverter.ToInt32(data, offset); //Task Name
                        string tttlkLookup = GlobalFindStrRefbyID(tTitle, game, CurrentLoadedExport.FileRef as ME1Package);
                        nTaskIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Task Name StrRef: {tTitle} { tttlkLookup }",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafObject
                        });
                        offset += 4;

                        int tDescription = BitConverter.ToInt32(data, offset); //Task Description
                        string tdtlkLookup = GlobalFindStrRefbyID(tDescription, game, CurrentLoadedExport.FileRef as ME1Package);
                        nTaskIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Task Description StrRef: {tDescription} { tdtlkLookup }",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafObject
                        });
                        offset += 4;

                        int piCount = BitConverter.ToInt32(data, offset); //Plot item Count
                        var PlotIDs = new BinInterpTreeItem
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
                            var nPlotItems = new BinInterpTreeItem
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
                        nTaskIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Planet Name: {CurrentLoadedExport.FileRef.getNameEntry(planetName)}_{planetIdx} ",
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
                        nTaskIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Waypoint ref: {wpRef} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafStr
                        });
                        offset += wpStrLgth;
                    }

                    int pCount = BitConverter.ToInt32(data, offset); //Plot Item Count
                    var PlotItemIDs = new BinInterpTreeItem
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
                        var nPlotItemIDs = new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Plot Item: {p} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        };
                        PlotItemIDs.Items.Add(nPlotItemIDs);

                        int iPlotInstVersion = BitConverter.ToInt32(data, offset);  //Plot Item Instance Version
                        nPlotItemIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Plot item Instance Version: {iPlotInstVersion} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int pTitle = BitConverter.ToInt32(data, offset); //Plot item Name
                        string pitlkLookup = GlobalFindStrRefbyID(pTitle, game, CurrentLoadedExport.FileRef as ME1Package);
                        nPlotItemIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Goal Name StrRef: {pTitle} { pitlkLookup }",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafObject
                        });
                        offset += 4;

                        int pIcon = BitConverter.ToInt32(data, offset); //Icon Index
                        nPlotItemIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Icon Index: {pIcon} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int pConditional = BitConverter.ToInt32(data, offset); //Conditional
                        nPlotItemIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Conditional: {pConditional} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int pState = BitConverter.ToInt32(data, offset); //Int
                        nPlotItemIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Integer State: {pState} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int pTarget = BitConverter.ToInt32(data, offset); //Target Index
                        nPlotItemIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Item Count Target: {pTarget} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;
                    }

                }

                int bsCount = BitConverter.ToInt32(data, offset);
                var bsNode = new BinInterpTreeItem
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
                    var BoolEvtIDs = new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Bool Journal Event: {iBoolEvtID} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    bsNode.Items.Add(BoolEvtIDs);

                    int bsInstVersion = BitConverter.ToInt32(data, offset); //Instance Version
                    var BoolQuestIDs = new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Instance Version: {bsInstVersion} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    BoolEvtIDs.Items.Add(BoolQuestIDs);

                    int bqstCount = BitConverter.ToInt32(data, offset); //Related Quests Count
                    var bqstNode = new BinInterpTreeItem
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
                        var bquestIDs = new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Related Quest: {bqQuest} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset -= 16;
                        bqstNode.Items.Add(bquestIDs);

                        int bqInstVersion = BitConverter.ToInt32(data, offset);  //Bool quest Instance Version
                        bquestIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Instance Version: {bqInstVersion} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int bqTask = BitConverter.ToInt32(data, offset);  //Bool quest Instance Version
                        bquestIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Related Task Link: {bqTask} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int bqState = BitConverter.ToInt32(data, offset);  //Bool quest State
                        bquestIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Bool State: {bqState} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int bqConditional = BitConverter.ToInt32(data, offset);  //Bool quest Conditional
                        bquestIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Conditional: {bqConditional} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;


                        bqQuest = BitConverter.ToInt32(data, offset);  //Bool quest ID
                        bquestIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Quest Link: {bqQuest} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;
                    }

                }

                int isCount = BitConverter.ToInt32(data, offset);
                var isNode = new BinInterpTreeItem
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
                    var IntEvtIDs = new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Int Journal Event: {iInttEvtID} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    isNode.Items.Add(IntEvtIDs);

                    int isInstVersion = BitConverter.ToInt32(data, offset); //Instance Version
                    var IntQuestIDs = new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Instance Version: {isInstVersion} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    IntEvtIDs.Items.Add(IntQuestIDs);

                    int iqstCount = BitConverter.ToInt32(data, offset); //Related Quests Count
                    var iqstNode = new BinInterpTreeItem
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
                        var iquestIDs = new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Related Quest: {iqQuest} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset -= 16;
                        iqstNode.Items.Add(iquestIDs);

                        int iqInstVersion = BitConverter.ToInt32(data, offset);  //Int quest Instance Version
                        iquestIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Instance Version: {iqInstVersion} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int iqTask = BitConverter.ToInt32(data, offset);  //Int quest Instance Version
                        iquestIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Related Task Link: {iqTask} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int iqState = BitConverter.ToInt32(data, offset);  //Int quest State
                        iquestIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Bool State: {iqState} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int iqConditional = BitConverter.ToInt32(data, offset);  //Int quest Conditional
                        iquestIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Conditional: {iqConditional} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        iqQuest = BitConverter.ToInt32(data, offset);  //Int quest ID
                        iquestIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Quest Link: {iqQuest} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;
                    }

                }

                int fsCount = BitConverter.ToInt32(data, offset);
                var fsNode = new BinInterpTreeItem
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
                    var FloatEvtIDs = new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Float Journal Event: {iFloatEvtID} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    fsNode.Items.Add(FloatEvtIDs);

                    int fsInstVersion = BitConverter.ToInt32(data, offset); //Instance Version
                    var FloatQuestIDs = new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Instance Version: {fsInstVersion} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    FloatEvtIDs.Items.Add(FloatQuestIDs);

                    int fqstCount = BitConverter.ToInt32(data, offset); //Related Quests Count
                    var fqstNode = new BinInterpTreeItem
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
                        var fquestIDs = new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Related Quest: {fqQuest} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset -= 16;
                        fqstNode.Items.Add(fquestIDs);

                        int fqInstVersion = BitConverter.ToInt32(data, offset);  //float quest Instance Version
                        fquestIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Instance Version: {fqInstVersion} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int fqTask = BitConverter.ToInt32(data, offset);  //Float quest Instance Version
                        fquestIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Related Task Link: {fqTask} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int fqState = BitConverter.ToInt32(data, offset);  //Float quest State
                        fquestIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Bool State: {fqState} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int fqConditional = BitConverter.ToInt32(data, offset);  //Float quest Conditional
                        fquestIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Conditional: {fqConditional} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        fqQuest = BitConverter.ToInt32(data, offset);  //Float quest ID
                        fquestIDs.Items.Add(new BinInterpTreeItem
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
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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
                var SectionsNode = new BinInterpTreeItem
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
                    var SectionIDs = new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Section ID: {iSectionID} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    SectionsNode.Items.Add(SectionIDs);

                    int instVersion = BitConverter.ToInt32(data, offset); //Instance Version
                    SectionIDs.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Instance Version: {instVersion} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int sTitle = BitConverter.ToInt32(data, offset); //Codex Title
                    string ttlkLookup = GlobalFindStrRefbyID(sTitle, game, CurrentLoadedExport.FileRef as ME1Package);
                    SectionIDs.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Section Title StrRef: {sTitle} { ttlkLookup }",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                    int sDescription = BitConverter.ToInt32(data, offset); //Codex Description
                    string dtlkLookup = GlobalFindStrRefbyID(sDescription, game, CurrentLoadedExport.FileRef as ME1Package);
                    SectionIDs.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Section Description StrRef: {sDescription} { dtlkLookup }",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                    int sTexture = BitConverter.ToInt32(data, offset); //Texture ID
                    SectionIDs.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Section Texture ID: {sTexture} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                    int sPriority = BitConverter.ToInt32(data, offset); //Priority
                    SectionIDs.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Section Priority: {sPriority}  (5 is low, 1 is high)",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                    if (instVersion >= 3)
                    {
                        int sndExport = BitConverter.ToInt32(data, offset);
                        SectionIDs.Items.Add(new BinInterpTreeItem
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
                    SectionIDs.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Is Primary Codex: {sPrimary}  { bPrimary }",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                }
                //START OF CODEX PAGES SECTION
                int pCount = BitConverter.ToInt32(data, offset);
                var PagesNode = new BinInterpTreeItem
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
                    var PageIDs = new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Page Bool: {iPageID} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    PagesNode.Items.Add(PageIDs);

                    int instVersion = BitConverter.ToInt32(data, offset); //Instance Version
                    PageIDs.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Instance Version: {instVersion} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int pTitle = BitConverter.ToInt32(data, offset); //Codex Title
                    string ttlkLookup = GlobalFindStrRefbyID(pTitle, game, CurrentLoadedExport.FileRef as ME1Package);
                    PageIDs.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Page Title StrRef: {pTitle} { ttlkLookup }",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int pDescription = BitConverter.ToInt32(data, offset); //Codex Description
                    string dtlkLookup = GlobalFindStrRefbyID(pDescription, game, CurrentLoadedExport.FileRef as ME1Package);
                    PageIDs.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Page Description StrRef: {pDescription} { dtlkLookup }",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int pTexture = BitConverter.ToInt32(data, offset); //Texture ID
                    PageIDs.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Section Texture ID: {pTexture} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int pPriority = BitConverter.ToInt32(data, offset); //Priority
                    PageIDs.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Section Priority: {pPriority}  (5 is low, 1 is high)",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    if (instVersion == 4) //ME3 use object reference found sound then section
                    {
                        int sndExport = BitConverter.ToInt32(data, offset);
                        PageIDs.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X8} Codex Sound: {sndExport} {CurrentLoadedExport.FileRef.GetEntryString(sndExport)}",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafObject
                        });
                        offset += 4;

                        int pSection = BitConverter.ToInt32(data, offset); //Section ID
                        PageIDs.Items.Add(new BinInterpTreeItem
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
                        PageIDs.Items.Add(new BinInterpTreeItem
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
                        PageIDs.Items.Add(new BinInterpTreeItem
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
                        PageIDs.Items.Add(new BinInterpTreeItem
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
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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
                var boneList = Pcc.getUExport(animsetData.Value).GetProperty<ArrayProperty<NameProperty>>("TrackBoneNames");
                Enum.TryParse(CurrentLoadedExport.GetProperty<EnumProperty>("RotationCompressionFormat").Value.Name, out AnimationCompressionFormat rotCompression);
                int offset = binarystart;

                int binLength = BitConverter.ToInt32(data, offset);
                var LengthNode = new BinInterpTreeItem
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
                    var BoneID = new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} Bone: {bone} {boneList[bone].Value}",
                        Name = "_" + offset,
                        Tag = NodeType.Unknown
                    };
                    subnodes.Add(BoneID);

                    for (int j = 0; j < bonePosCount; j++)
                    {
                        offset = animBinStart + bonePosOffset + j * 12;
                        var PosKeys = new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} PosKey {j}",
                            Name = "_" + offset,
                            Tag = NodeType.Unknown
                        };
                        BoneID.Items.Add(PosKeys);


                        var posX = BitConverter.ToSingle(data, offset);
                        PosKeys.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} X: {posX} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafFloat
                        });
                        offset += 4;
                        var posY = BitConverter.ToSingle(data, offset);
                        PosKeys.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Y: {posY} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafFloat
                        });
                        offset += 4;
                        var posZ = BitConverter.ToSingle(data, offset);
                        PosKeys.Items.Add(new BinInterpTreeItem
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
                            var RotKeys = new BinInterpTreeItem
                            {
                                Header = $"0x{offsetRotX:X5} RotKey {j}",
                                Name = "_" + offsetRotX,
                                Tag = NodeType.Unknown
                            };
                            BoneID.Items.Add(RotKeys);
                            RotKeys.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offsetRotX:X5} RotX: {rotX} ",
                                Name = "_" + offsetRotX,
                                Tag = NodeType.StructLeafFloat
                            });
                            RotKeys.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offsetRotY:X5} RotY: {rotY} ",
                                Name = "_" + offsetRotY,
                                Tag = NodeType.StructLeafFloat
                            });
                            RotKeys.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offsetRotZ:X5} RotZ: {rotZ} ",
                                Name = "_" + offsetRotZ,
                                Tag = NodeType.StructLeafFloat
                            });
                            if (rotCompression == AnimationCompressionFormat.ACF_None)
                            {
                                RotKeys.Items.Add(new BinInterpTreeItem
                                {
                                    Header = $"0x{offsetRotW:X5} RotW: {rotW} ",
                                    Name = "_" + offsetRotW,
                                    Tag = NodeType.StructLeafFloat
                                });
                            }
                        }
                        else
                        {

                            BoneID.Items.Add(new BinInterpTreeItem
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
                subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
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
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Magic: {bin.ReadInt32():X8}") { Length = 4 });
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32():X8}") { Length = 4 });
                if (Pcc.Game == MEGame.ME3)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32():X8}") { Length = 4 });
                }

                if (Pcc.Game == MEGame.ME2)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                }
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Licensee: {bin.ReadStringASCII(bin.ReadInt32())}"));
                if (Pcc.Game == MEGame.ME2)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                }
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Project: {bin.ReadStringASCII(bin.ReadInt32())}"));
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32():X8}") { Length = 4 });
                if (Pcc.Game == MEGame.ME2)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32():X8}") { Length = 4 });
                }
                else
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                }

                if (Pcc.Game != MEGame.ME2)
                {
                    int hNodeCount = bin.ReadInt32();
                    var hNodes = new List<ITreeItem>();
                    subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Nodes: {hNodeCount} items")
                    {
                        Items = hNodes
                    });
                    for (int i = 0; i < hNodeCount; i++)
                    {
                        var hNodeNodes = new List<ITreeItem>();
                        hNodes.Add(new BinInterpTreeItem(bin.Position, $"{i}")
                        {
                            Items = hNodeNodes
                        });
                        hNodeNodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                        var nNameCount = bin.ReadInt32();
                        hNodeNodes.Add(new BinInterpTreeItem(bin.Position, $"Name Count: {nNameCount}") { Length = 4 });
                        for (int n = 0; n < nNameCount; n++)
                        {
                            hNodeNodes.Add(new BinInterpTreeItem(bin.Position, $"Name: {bin.ReadStringASCII(bin.ReadInt32())}"));
                            hNodeNodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                        }
                    }
                }

                int nameCount = bin.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Names: {nameCount} items")
                {
                    //ME2 different to ME3/1
                    Items = ReadList(nameCount, i => new BinInterpTreeItem(bin.Skip(Pcc.Game != MEGame.ME2 ? 0 : 4).Position, $"{bin.ReadStringASCII(bin.ReadInt32())}"))
                });

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                if (Pcc.Game == MEGame.ME2)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                }

                int lineCount = bin.ReadInt32();
                var lines = new List<ITreeItem>();

                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"FaceFXLines: {lineCount} items")
                {
                    Items = lines
                });
                for (int i = 0; i < lineCount; i++)
                {
                    var nodes = new List<ITreeItem>();
                    lines.Add(new BinInterpTreeItem(bin.Position, $"{i}")
                    {
                        Items = nodes
                    });
                    if (Pcc.Game == MEGame.ME2)
                    {
                        nodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                        nodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    }
                    nodes.Add(new BinInterpTreeItem(bin.Position, $"Name: {bin.ReadInt32()}") { Length = 4 });
                    if (Pcc.Game == MEGame.ME2)
                    {
                        nodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                    }
                    int animationCount = bin.ReadInt32();
                    var anims = new List<ITreeItem>();
                    nodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Animations: {animationCount} items")
                    {
                        Items = anims
                    });
                    for (int j = 0; j < animationCount; j++)
                    {
                        var animNodes = new List<ITreeItem>();
                        anims.Add(new BinInterpTreeItem(bin.Position, $"{j}")
                        {
                            Items = animNodes
                        });
                        if (Pcc.Game == MEGame.ME2)
                        {

                            animNodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                            animNodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                        }
                        animNodes.Add(new BinInterpTreeItem(bin.Position, $"Index: {bin.ReadInt32()}") { Length = 4 });
                        animNodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                        if (Pcc.Game == MEGame.ME2)
                        {
                            animNodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                        }
                    }

                    int pointsCount = bin.ReadInt32();
                    nodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Points: {pointsCount} items")
                    {
                        Items = ReadList(pointsCount, j => new BinInterpTreeItem(bin.Position, $"{j}")
                        {
                            Items = new List<ITreeItem>
                            {
                                new BinInterpTreeItem(bin.Position, $"Time: {bin.ReadFloat()}") {Length = 4},
                                new BinInterpTreeItem(bin.Position, $"Weight: {bin.ReadFloat()}") {Length = 4},
                                new BinInterpTreeItem(bin.Position, $"InTangent: {bin.ReadFloat()}") {Length = 4},
                                new BinInterpTreeItem(bin.Position, $"LeaveTangent: {bin.ReadFloat()}") {Length = 4}
                            }
                        })
                    });

                    if (pointsCount > 0)
                    {
                        if (Pcc.Game == MEGame.ME2)
                        {
                            nodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                        }
                        nodes.Add(new BinInterpTreeItem(bin.Position, $"NumKeys: {bin.ReadInt32()} items")
                        {
                            Items = ReadList(bin.Skip(-4).ReadInt32(), j => new BinInterpTreeItem(bin.Position, $"{bin.ReadInt32()} keys"))
                        });
                    }
                    nodes.Add(new BinInterpTreeItem(bin.Position, $"Fade In Time: {bin.ReadFloat()}") { Length = 4 });
                    nodes.Add(new BinInterpTreeItem(bin.Position, $"Fade Out Time: {bin.ReadFloat()}") { Length = 4 });
                    nodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                    if (Pcc.Game == MEGame.ME2)
                    {
                        nodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                        nodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    }
                    nodes.Add(new BinInterpTreeItem(bin.Position, $"Path: {bin.ReadStringASCII(bin.ReadInt32())}"));
                    if (Pcc.Game == MEGame.ME2)
                    {
                        nodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    }
                    nodes.Add(new BinInterpTreeItem(bin.Position, $"ID: {bin.ReadStringASCII(bin.ReadInt32())}"));
                    nodes.Add(new BinInterpTreeItem(bin.Position, $"index: {bin.ReadInt32()}") { Length = 4 });
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
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
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Magic: {bin.ReadInt32():X8}") { Length = 4 });
                int versionID = bin.ReadInt32(); //1710 = ME1, 1610 = ME2, 1731 = ME3.
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Version: {versionID} {versionID:X8}") { Length = 4 });
                if (versionID == 1731)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32():X8}") { Length = 4 });
                }

                if (versionID == 1610)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                }
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Licensee: {bin.ReadStringASCII(bin.ReadInt32())}"));
                if (versionID == 1610)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                }
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Project: {bin.ReadStringASCII(bin.ReadInt32())}"));
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32():X8}") { Length = 4 });
                if (versionID == 1610)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32():X8}") { Length = 4 });
                }
                else
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                }
                //Node Table
                if (versionID != 1610)
                {
                    int hNodeCount = bin.ReadInt32();
                    var hNodes = new List<ITreeItem>();
                    subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Nodes: {hNodeCount} items")
                    {
                        Items = hNodes
                    });
                    for (int i = 0; i < hNodeCount; i++)
                    {
                        var hNodeNodes = new List<ITreeItem>();
                        hNodes.Add(new BinInterpTreeItem(bin.Position, $"{i}")
                        {
                            Items = hNodeNodes
                        });
                        hNodeNodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                        var nNameCount = bin.ReadInt32();
                        hNodeNodes.Add(new BinInterpTreeItem(bin.Position, $"Name Count: {nNameCount}") { Length = 4 });
                        for (int n = 0; n < nNameCount; n++)
                        {
                            hNodeNodes.Add(new BinInterpTreeItem(bin.Position, $"Name: {bin.ReadStringASCII(bin.ReadInt32())}"));
                            hNodeNodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
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
                    nametabObj.Add(new BinInterpTreeItem(bin.Skip(versionID != 1610 ? 0 : 4).Position, $"{m}: {mName}"));
                }

                subnodes.Add(new BinInterpTreeItem(nametablePos, $"Names: {nameCount} items")
                {
                    //ME1 and ME3 same, ME2 different
                    Items = nametabObj
                });

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });


                //FROM HERE ME3 ONLY WIP
                //LIST A
                var unkListA = new List<ITreeItem>();
                var countA = bin.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Unknown Table A: {countA} items")
                {
                    Items = unkListA
                });

                for (int a = 0; a < countA; a++) //NOT EXACT??
                {
                    var tableItems = new List<ITreeItem>();
                    unkListA.Add(new BinInterpTreeItem(bin.Position, $"Table Index: {bin.ReadInt32()}")
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
                            tableItems.Add(new BinInterpTreeItem(bin.Position - 4, $"End Marker: FF FF FF 7F") { Length = 4 });
                            tableItems.Add(new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                            iscontinuing = false;
                            break;
                        }
                        else
                        {
                            bin.Position = loc;
                            tableItems.Add(new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                        }

                    }
                    //Name list to Bones and other facefx?
                    var unkNameList1 = new List<ITreeItem>();
                    var countUk1 = bin.ReadInt32();
                    tableItems.Add(new BinInterpTreeItem(bin.Position - 4, $"Unknown Name List: {countUk1} items")
                    {
                        Items = unkNameList1
                    });
                    for (int b = 0; b < countUk1; b++)
                    {
                        var unameVal = bin.ReadInt32();
                        var unkNameList1items = new List<ITreeItem>();
                        unkNameList1.Add(new BinInterpTreeItem(bin.Position - 4, $"Name {b}: {unameVal} {nameTable[unameVal]}")
                        {
                            Items = unkNameList1items
                        });
                        unkNameList1items.Add(new BinInterpTreeItem(bin.Position, $"Table index: {bin.ReadInt32()}") { Length = 4 });
                        unkNameList1items.Add(new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                        unkNameList1items.Add(new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                        unkNameList1items.Add(new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                        unkNameList1items.Add(new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                        unkNameList1items.Add(new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                        unkNameList1items.Add(new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                        unkNameList1items.Add(new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                        unkNameList1items.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                        unkNameList1items.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                        unkNameList1items.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                    }
                }

                //LIST B
                var unkListB = new List<ITreeItem>();
                var countB = bin.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Unknown Table B: {countB} items")
                {
                    Items = unkListB
                });

                for (int b = 0, i = 0; i < countB; b++, i++)
                {
                    var bLocation = bin.Position;
                    var firstval = bin.ReadInt32();  //maybe version id?
                    var bIdxVal = bin.ReadInt32();
                    var unkListBitems = new List<ITreeItem>();
                    unkListB.Add(new BinInterpTreeItem(bin.Position - 4, $"{b}: Table Index: {bIdxVal} : {nameTable[bIdxVal]}")
                    {
                        Items = unkListBitems
                    });
                    switch (firstval)
                    {
                        case 2:
                            unkListBitems.Add(new BinInterpTreeItem(bin.Position - 8, $"Version??: {firstval}"));
                            unkListBitems.Add(new BinInterpTreeItem(bin.Position - 4, $"Table index: {bIdxVal}"));
                            unkListBitems.Add(new BinInterpTreeItem(bin.Position, $"Unknown int: {bin.ReadInt32()}") { Length = 4 });
                            unkListBitems.Add(new BinInterpTreeItem(bin.Position, $"Unknown int: {bin.ReadInt32()}") { Length = 4 });
                            unkListBitems.Add(new BinInterpTreeItem(bin.Position, $"Unknown int: {bin.ReadInt32()}") { Length = 4 });
                            break;
                        default:
                            unkListBitems.Add(new BinInterpTreeItem(bin.Position - 8, $"Version??: {firstval}"));
                            unkListBitems.Add(new BinInterpTreeItem(bin.Position - 4, $"Table index: {bIdxVal}"));
                            int flagMaybe = bin.ReadInt32();
                            unkListBitems.Add(new BinInterpTreeItem(bin.Position - 4, $"another version?: {flagMaybe}") { Length = 4 });
                            unkListBitems.Add(new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                            unkListBitems.Add(new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                            unkListBitems.Add(new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                            bool hasNameList;
                            if (flagMaybe == 0)
                            {
                                unkListBitems.Add(new BinInterpTreeItem(bin.Position, $"Unknown int: {bin.ReadInt32()}"));
                                hasNameList = true;
                            }
                            else
                            {
                                var unkStringLength = bin.ReadInt32();
                                unkListBitems.Add(new BinInterpTreeItem(bin.Position - 4, $"Unknown String: {bin.ReadStringASCII(unkStringLength)}"));
                                hasNameList = unkStringLength == 0;
                            }
                            if (hasNameList)
                            {
                                var unkNameList2 = new List<ITreeItem>(); //Name list to Bones and other facefx phenomes?
                                var countUk2 = bin.ReadInt32();
                                unkListBitems.Add(new BinInterpTreeItem(bin.Position - 4, $"Unknown Name List: {countUk2} items")
                                {
                                    Items = unkNameList2
                                });
                                for (int n2 = 0; n2 < countUk2; n2++)
                                {
                                    var unameVal = bin.ReadInt32();
                                    var unkNameList2items = new List<ITreeItem>();
                                    unkNameList2.Add(new BinInterpTreeItem(bin.Position - 4, $"Name: {unameVal} {nameTable[unameVal]}")
                                    {
                                        Items = unkNameList2items
                                    });
                                    unkNameList2items.Add(new BinInterpTreeItem(bin.Position, $"Unknown int: {bin.ReadInt32()}") { Length = 4 });
                                    var n3count = bin.ReadInt32();
                                    unkNameList2items.Add(new BinInterpTreeItem(bin.Position - 4, $"Unknown count: {n3count}"));
                                    for (int n3 = 0; n3 < n3count; n3++)
                                    {
                                        unkNameList2items.Add(new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                                    }
                                }
                                if (firstval != 6)
                                {
                                    unkListBitems.Add(new BinInterpTreeItem(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
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
                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Unknown Table C")
                {
                    Items = unkListC
                });

                for (int c = 0; c < countB; c++)
                {
                    var unkListCitems = new List<ITreeItem>();
                    unkListC.Add(new BinInterpTreeItem(bin.Position, $"{c}")
                    {
                        Items = unkListCitems
                    });
                    int name = bin.ReadInt32();
                    unkListCitems.Add(new BinInterpTreeItem(bin.Position, $"Name?: {name} {nameTable[name]}") { Length = 4 });
                    unkListCitems.Add(new BinInterpTreeItem(bin.Position, $"Unknown int: {bin.ReadInt32()}") { Length = 4 });
                    int stringCount = bin.ReadInt32();
                    unkListCitems.Add(new BinInterpTreeItem(bin.Position - 4, $"Unknown int: {stringCount}") { Length = 4 });
                    unkListCitems.Add(new BinInterpTreeItem(bin.Position, $"Unknown String: {bin.ReadStringASCII(bin.ReadInt32())}"));
                    for (int i = 1; i < stringCount; i++)
                    {
                        c++;
                        unkListCitems.Add(new BinInterpTreeItem(bin.Position, $"Unknown int: {bin.ReadInt32()}") { Length = 4 });
                        unkListCitems.Add(new BinInterpTreeItem(bin.Position, $"Unknown String: {bin.ReadStringASCII(bin.ReadInt32())}"));
                    }

                }



                if (versionID == 1610)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                }

                int lineCount = bin.ReadInt32();
                var lines = new List<ITreeItem>();

                subnodes.Add(new BinInterpTreeItem(bin.Position - 4, $"FaceFXLines: {lineCount} items")
                {
                    Items = lines
                });
                for (int i = 0; i < lineCount; i++)
                {
                    var nodes = new List<ITreeItem>();
                    lines.Add(new BinInterpTreeItem(bin.Position, $"{i}")
                    {
                        Items = nodes
                    });
                    nodes.Add(new BinInterpTreeItem(bin.Position, $"Name: {nameTable[bin.ReadInt32()]}") { Length = 4 });
                    nodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                    nodes.Add(new BinInterpTreeItem(bin.Position, $"Name: {nameTable[bin.ReadInt32()]}") { Length = 4 });
                    int animationCount = bin.ReadInt32();
                    var anims = new List<ITreeItem>();
                    nodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Animations: {animationCount} items")
                    {
                        Items = anims
                    });
                    for (int j = 0; j < animationCount; j++)
                    {
                        var animNodes = new List<ITreeItem>();
                        anims.Add(new BinInterpTreeItem(bin.Position, $"{j}")
                        {
                            Items = animNodes
                        });
                        animNodes.Add(new BinInterpTreeItem(bin.Position, $"Name: {nameTable[bin.ReadInt32()]}") { Length = 4 });
                        animNodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                        if (versionID == 1610)
                        {
                            animNodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                        }
                    }

                    if (animationCount > 0)
                    {
                        int pointsCount = bin.ReadInt32();
                        nodes.Add(new BinInterpTreeItem(bin.Position - 4, $"Points: {pointsCount} items")
                        {
                            Items = ReadList(pointsCount, j => new BinInterpTreeItem(bin.Position, $"{j}")
                            {
                                Items = new List<ITreeItem>
                                {
                                    new BinInterpTreeItem(bin.Position, $"Time: {bin.ReadFloat()}") {Length = 4},
                                    new BinInterpTreeItem(bin.Position, $"Weight: {bin.ReadFloat()}") {Length = 4},
                                    new BinInterpTreeItem(bin.Position, $"InTangent: {bin.ReadFloat()}") {Length = 4},
                                    new BinInterpTreeItem(bin.Position, $"LeaveTangent: {bin.ReadFloat()}") {Length = 4}
                                }
                            })
                        });

                        if (pointsCount > 0)
                        {
                            if (versionID == 1610)
                            {
                                nodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                            }
                            nodes.Add(new BinInterpTreeItem(bin.Position, $"NumKeys: {bin.ReadInt32()} items")
                            {
                                Items = ReadList(bin.Skip(-4).ReadInt32(), j => new BinInterpTreeItem(bin.Position, $"{bin.ReadInt32()} keys"))
                            });
                        }
                    }
                    nodes.Add(new BinInterpTreeItem(bin.Position, $"Fade In Time: {bin.ReadFloat()}") { Length = 4 });
                    nodes.Add(new BinInterpTreeItem(bin.Position, $"Fade Out Time: {bin.ReadFloat()}") { Length = 4 });
                    nodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt32()}") { Length = 4 });
                    if (versionID == 1610)
                    {
                        nodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                        nodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    }
                    nodes.Add(new BinInterpTreeItem(bin.Position, $"Path: {bin.ReadStringASCII(bin.ReadInt32())}"));
                    if (versionID == 1610)
                    {
                        nodes.Add(new BinInterpTreeItem(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    }
                    nodes.Add(new BinInterpTreeItem(bin.Position, $"ID: {bin.ReadStringASCII(bin.ReadInt32())}"));
                    nodes.Add(new BinInterpTreeItem(bin.Position, $"index: {bin.ReadInt32()}") { Length = 4 });
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
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
                subnodes.Add(new BinInterpTreeItem
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
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} NextItemCompilingChain: {classObjTree} {CurrentLoadedExport.FileRef.GetEntryString(classObjTree)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int childObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
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
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<ITreeItem> StartBioGestureRuntimeDataScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                int offset = binarystart;
                int count = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"{offset:X4} Count: {count}",
                    Name = "_" + offset.ToString()
                });
                offset += 4;
                for (int i = 0; i < count; i++)
                {
                    int name1 = BitConverter.ToInt32(data, offset);
                    int name2 = BitConverter.ToInt32(data, offset + 8);
                    string text = $"{offset:X4} Item {i}: {CurrentLoadedExport.FileRef.getNameEntry(name1)} => {CurrentLoadedExport.FileRef.getNameEntry(name2)}";
                    subnodes.Add(new BinInterpTreeItem
                    {
                        Header = text,
                        Name = "_" + offset.ToString()
                    });
                    offset += 16;
                }

                int idx = 0;
                MemoryStream dataAsStream = new MemoryStream(data);
                while (offset < data.Length)
                {
                    var node = new BinInterpTreeItem
                    {
                        Header = $"{offset:X4} Item {idx}",
                        Name = "_" + offset.ToString()
                    };
                    subnodes.Add(node);

                    int unk1 = BitConverter.ToInt32(data, offset);
                    node.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{offset:X4} Unk1: {unk1}",
                        Name = "_" + offset.ToString()
                    });
                    offset += 4;
                    int unk2 = BitConverter.ToInt32(data, offset);
                    node.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{offset:X4} Name Unk2: {unk2} {CurrentLoadedExport.FileRef.getNameEntry(unk2)}",
                        Name = "_" + offset.ToString()
                    });
                    offset += 8;
                    int unk3 = BitConverter.ToInt32(data, offset);
                    node.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{offset:X4} Name Unk3: {unk3} {CurrentLoadedExport.FileRef.getNameEntry(unk3)}",
                        Name = "_" + offset.ToString()
                    });
                    offset += 8;

                    dataAsStream.Position = offset;
                    int strLength = dataAsStream.ReadValueS32();
                    string str = Gibbed.IO.StreamHelpers.ReadString(dataAsStream, strLength * -2, true, Encoding.Unicode);
                    node.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{offset:X4}: {str}",
                        Name = "_" + offset.ToString()
                    });
                    offset = (int)dataAsStream.Position;
                    int unk4 = BitConverter.ToInt32(data, offset);
                    node.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{offset:X4} Name Unk4: {unk4} {CurrentLoadedExport.FileRef.getNameEntry(unk4)}",
                        Name = "_" + offset.ToString()
                    });
                    offset += 8;
                    idx++;
                    break;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }
        private List<ITreeItem> StartObjectRedirectorScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();

            int redirnum = BitConverter.ToInt32(data, binarystart);
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"{binarystart:X4} Redirect references to this export to: {redirnum} {CurrentLoadedExport.FileRef.getEntry(redirnum).GetFullPath}",
                Name = "_" + binarystart.ToString()
            });
            return subnodes;
        }

        private List<ITreeItem> StartObjectScan(byte[] data)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                int offset = 0; //this property starts at 0 for parsing
                int unrealExportIndex = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Unreal Unique Index: {unrealExportIndex}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int noneUnrealProperty = BitConverter.ToInt32(data, offset);
                //int noneUnrealPropertyIndex = BitConverter.ToInt32(data, offset + 4);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Unreal property None Name: {CurrentLoadedExport.FileRef.getNameEntry(noneUnrealProperty)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafName
                });
                offset += 8;

                int superclassIndex = BitConverter.ToInt32(data, offset);
                string superclassStr = CurrentLoadedExport.FileRef.GetEntryString(superclassIndex);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Superclass: {superclassIndex}({superclassStr})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Next item in compiling chain UIndex: {classObjTree} {CurrentLoadedExport.FileRef.GetEntryString(classObjTree)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int unk1 = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Unknown 1: {unk1}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                UnrealFlags.EPropertyFlags ObjectFlagsMask = (UnrealFlags.EPropertyFlags)BitConverter.ToUInt64(data, offset);
                BinInterpTreeItem objectFlagsNode = new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} ObjectFlags: 0x{(ulong)ObjectFlagsMask:X16}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt,
                    IsExpanded = true
                };

                subnodes.Add(objectFlagsNode);

                //Create objectflags tree
                foreach (UnrealFlags.EPropertyFlags flag in Enums.GetValues<UnrealFlags.EPropertyFlags>())
                {
                    if ((ObjectFlagsMask & flag) != UnrealFlags.EPropertyFlags.None)
                    {
                        string reason = UnrealFlags.propertyflagsdesc[flag];
                        objectFlagsNode.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{(ulong)flag:X16} {flag} {reason}",
                            Name = "_" + offset
                        });
                    }
                }
                offset += 8;



                //has listed outerclass
                int none = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} None: {CurrentLoadedExport.FileRef.getNameEntry(none)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 8;

                int unk2 = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Unknown2: {unk2}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4; //

                switch (CurrentLoadedExport.ClassName)
                {
                    case "ByteProperty":
                    case "StructProperty":
                    case "ObjectProperty":
                    case "ComponentProperty":
                        {
                            if ((ObjectFlagsMask & UnrealFlags.EPropertyFlags.RepRetry) != 0)
                            {
                                offset += 2;
                            }
                            //has listed outerclass
                            int outer = BitConverter.ToInt32(data, offset);
                            subnodes.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} OuterClass: {outer} {CurrentLoadedExport.FileRef.GetEntryString(outer)}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;
                        }
                        break;
                    case "ArrayProperty":
                        {
                            int outer = BitConverter.ToInt32(data, offset);
                            subnodes.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Array can hold objects of type: {outer} {CurrentLoadedExport.FileRef.GetEntryString(outer)}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;
                        }
                        break;
                    case "ClassProperty":
                        {

                            //has listed outerclass
                            int outer = BitConverter.ToInt32(data, offset);
                            subnodes.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Outer class: {outer} {CurrentLoadedExport.FileRef.GetEntryString(outer)}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            //type of class
                            int classtype = BitConverter.ToInt32(data, offset);
                            subnodes.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{offset:X5} Class type: {classtype} {CurrentLoadedExport.FileRef.GetEntryString(classtype)}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafObject
                            });
                            offset += 4;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<ITreeItem> Scan_WwiseStreamBank(byte[] data)
        {
            /*
             * int32 0?
             * stream length in AFC +4 | (bank size)
             * stream length in AFC +4 | (repeat) (bank size)
             * stream offset in AFC +4 | (bank offset in file)
             */
            var subnodes = new List<ITreeItem>();
            try
            {
                int pos = 0;
                switch (CurrentLoadedExport.FileRef.Game)
                {
                    case MEGame.ME3:
                        pos = CurrentLoadedExport.propsEnd();
                        break;
                    case MEGame.ME2:
                        pos = CurrentLoadedExport.propsEnd() + 0x20;
                        break;
                }

                int unk1 = BitConverter.ToInt32(data, pos);
                int DataSize = BitConverter.ToInt32(data, pos + 4);
                int DataSize2 = BitConverter.ToInt32(data, pos + 8);
                int DataOffset = BitConverter.ToInt32(data, pos + 0xC);

                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} Unknown: {unk1}",
                    Name = "_" + pos,
                });
                pos += 4;
                string dataset1type = CurrentLoadedExport.ClassName == "WwiseStream" ? "Stream length" : "Bank size";
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"{DataSize:X4} : {dataset1type} {DataSize} (0x{DataSize:X})",
                    Name = "_" + pos,
                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"{ pos:X4} {dataset1type}: {DataSize2} (0x{ DataSize2:X})",
                    Name = "_" + pos,
                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                string dataset2type = CurrentLoadedExport.ClassName == "WwiseStream" ? "Stream offset" : "Bank offset";
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} {dataset2type} in file: {DataOffset} (0x{DataOffset:X})",
                    Name = "_" + pos,
                    Tag = NodeType.StructLeafInt
                });

                if (CurrentLoadedExport.ClassName == "WwiseBank")
                {
                    //if (CurrentLoadedExport.DataOffset < DataOffset && (CurrentLoadedExport.DataOffset + CurrentLoadedExport.DataSize) < DataOffset)
                    //{
                    subnodes.Add(new BinInterpTreeItem
                    {
                        Header = "Click here to jump to the calculated end offset of wwisebank in this export",
                        Name = "_" + (DataSize2 + CurrentLoadedExport.propsEnd() + 16),
                        Tag = NodeType.Unknown
                    });
                    //}
                }

                pos += 4;
                switch (CurrentLoadedExport.ClassName)
                {
                    case "WwiseStream" when pos < data.Length && CurrentLoadedExport.GetProperty<NameProperty>("Filename") == null:
                        {
                            subnodes.Add(new BinInterpTreeItem
                            {
                                Header = $"{pos:X4} Embedded sound data. Use Soundplorer to modify this data.",
                                Name = "_" + pos,
                                Tag = NodeType.Unknown
                            });
                            subnodes.Add(new BinInterpTreeItem
                            {
                                Header = "The stream offset to this data will be automatically updated when this file is saved.",
                                Tag = NodeType.Unknown
                            });
                            break;
                        }
                    case "WwiseBank":
                        subnodes.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Embedded soundbank. Use Soundplorer WPF to view data.",
                            Name = "_" + pos,
                            Tag = NodeType.Unknown
                        });
                        subnodes.Add(new BinInterpTreeItem
                        {
                            Header = "The bank offset to this data will be automatically updated when this file is saved.",
                            Tag = NodeType.Unknown
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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
                    subnodes.Add(new BinInterpTreeItem { Header = $"0x{binarystart:X4} Count: {count.ToString()}", Name = "_" + binarystart });
                    binarystart += 4; //+ int
                    for (int i = 0; i < count; i++)
                    {
                        string nodeText = $"0x{binarystart:X4} ";
                        int val = BitConverter.ToInt32(data, binarystart);
                        string name = val.ToString();
                        if (val > 0 && val <= CurrentLoadedExport.FileRef.Exports.Count)
                        {
                            ExportEntry exp = CurrentLoadedExport.FileRef.Exports[val - 1];
                            nodeText += $"{i}: {name} {exp.PackageFullName}.{exp.ObjectName} ({exp.ClassName})";
                        }
                        else if (val < 0 && val != int.MinValue && Math.Abs(val) <= CurrentLoadedExport.FileRef.Imports.Count)
                        {
                            int csImportVal = Math.Abs(val) - 1;
                            ImportEntry imp = CurrentLoadedExport.FileRef.Imports[csImportVal];
                            nodeText += $"{i}: {name} {imp.PackageFullName}.{imp.ObjectName} ({imp.ClassName})";
                        }

                        subnodes.Add(new BinInterpTreeItem
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
                    subnodes.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{binarystart:X4} WwiseEventID: {wwiseID[0]:X2}{wwiseID[1]:X2}{wwiseID[2]:X2}{wwiseID[3]:X2}",
                        Tag = NodeType.Unknown,
                        Name = "_" + binarystart
                    });
                    binarystart += 4;

                    int count = BitConverter.ToInt32(data, binarystart);
                    var Streams = new BinInterpTreeItem
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
                        subnodes.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{binarystart:X4} BankCount: {bankcount}",
                            Tag = NodeType.StructLeafInt,
                            Name = "_" + binarystart
                        });
                        binarystart += 4;
                        for (int b = 0; b < bankcount; b++)
                        {
                            int bank = BitConverter.ToInt32(data, binarystart);
                            subnodes.Add(new BinInterpTreeItem
                            {
                                Header = $"0x{binarystart:X4} WwiseBank: {bank} {CurrentLoadedExport.FileRef.GetEntryString(bank)}",
                                Tag = NodeType.StructLeafObject,
                                Name = "_" + binarystart
                            });
                            binarystart += 4;
                        }

                        int streamcount = BitConverter.ToInt32(data, binarystart);
                        subnodes.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{binarystart:X4} StreamCount: {streamcount}",
                            Tag = NodeType.StructLeafInt,
                            Name = "_" + binarystart
                        });
                        binarystart += 4;
                        for (int w = 0; w < streamcount; w++)
                        {
                            int wwstream = BitConverter.ToInt32(data, binarystart);
                            subnodes.Add(new BinInterpTreeItem
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
                    subnodes.Add(new BinInterpTreeItem("Only ME3 and ME2 are supported for this scan."));
                    return subnodes;
                }

            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
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
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X4} Count: {count.ToString()}"
                });
                binarypos += 4; //+ int
                for (int i = 0; i < count; i++)
                {
                    int nameIndex = BitConverter.ToInt32(data, binarypos);
                    int nameIndexNum = BitConverter.ToInt32(data, binarypos + 4);
                    int shouldBe1 = BitConverter.ToInt32(data, binarypos + 8);

                    //TODO: Relink this property on package porting!
                    var name = CurrentLoadedExport.FileRef.getNameEntry(nameIndex);
                    string nodeValue = $"{(name == "INVALID NAME VALUE " + nameIndex ? "" : name)}_{nameIndexNum}";
                    if (shouldBe1 != 1)
                    {
                        //ERROR
                        nodeValue += " - Not followed by 1 (integer)!";
                    }

                    subnodes.Add(new BinInterpTreeItem
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
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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
                    subnodes.Add(new BinInterpTreeItem
                    {
                        Header = $"{binarystart:X4} Length: {length}",
                        Name = $"_{pos.ToString()}"
                    });
                    pos += 4;
                    if (length != 0)
                    {
                        int nameindex = BitConverter.ToInt32(data, pos);
                        int nameindexunreal = BitConverter.ToInt32(data, pos + 4);

                        string name = CurrentLoadedExport.FileRef.getNameEntry(nameindex);
                        subnodes.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Camera: {name}_{nameindexunreal}",
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
                        subnodes.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Count: {count}",
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
                                nameindexunreal = BitConverter.ToInt32(data, pos + 4);
                                BinInterpTreeItem parentnode = new BinInterpTreeItem
                                {
                                    Header = $"{pos:X4} Camera {i + 1}: {CurrentLoadedExport.FileRef.getNameEntry(nameindex)}_{nameindexunreal}",
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
                            subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
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
                int offset = 0;

                int unrealExportIndex = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Unreal Unique Index: {unrealExportIndex}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;


                int superclassIndex = BitConverter.ToInt32(data, offset);
                string superclassStr = CurrentLoadedExport.FileRef.GetEntryString(superclassIndex);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Superclass Index: {superclassIndex}({superclassStr})",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int unknown1 = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Unknown 1: {unknown1}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int childProbeUIndex = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Child probe first item UIndex: {childProbeUIndex} ({CurrentLoadedExport.FileRef.GetEntryString(childProbeUIndex)}))",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;


                //I am not sure what these mean. However if Pt1&2 are 33/25, the following bytes that follow are extended.
                //int headerUnknown1 = BitConverter.ToInt32(data, offset);
                Int64 ignoreMask = BitConverter.ToInt64(data, offset);
                subnodes.Add(new BinInterpTreeItem
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
                var scriptBlock = new BinInterpTreeItem
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
                            scriptText += "0x" + t.pos.ToString("X4") + " " + t.text + "\n";
                        }

                        scriptBlock.Items.Add(new BinInterpTreeItem
                        {
                            Header = scriptText,
                            Name = "_" + offset
                        });
                    }
                    catch (Exception) { }

                }


                offset += skipAmount + 10; //heuristic to find end of script
                                           //for (int i = 0; i < 5; i++)
                                           //{
                uint stateMask = BitConverter.ToUInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Statemask: {stateMask} [{getStateFlagsStr(stateMask)}]",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;
                //}
                //offset += 2; //oher unknown
                int localFunctionsTableCount = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
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
                    (subnodes.Last() as BinInterpTreeItem).Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset - 12:X5}  {CurrentLoadedExport.FileRef.getNameEntry(nameTableIndex)}() = {functionObjectIndex} ({CurrentLoadedExport.FileRef.GetEntryString(functionObjectIndex)})",
                        Name = "_" + (offset - 12),
                        Tag = NodeType.StructLeafName //might need to add a subnode for the 3rd int
                    });
                }

                UnrealFlags.EClassFlags ClassFlags = (UnrealFlags.EClassFlags)BitConverter.ToUInt32(data, offset);

                var classFlagsNode = new BinInterpTreeItem()
                {
                    Header = $"0x{offset:X5} Class Mask: 0x{((int)ClassFlags):X8}",
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
                        classFlagsNode.Items.Add(new BinInterpTreeItem
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

                subnodes.Add(new BinInterpTreeItem
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
                    string postCompName = CurrentLoadedExport.FileRef.getNameEntry(postComponentsNoneNameIndex); //This appears to be unused in ME#, it is always None it seems.
                                                                                                                 /*if (postCompName != "None")
                                                                                                                 {
                                                                                                                     Debugger.Break();
                                                                                                                 }*/
                    subnodes.Add(new BinInterpTreeItem
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
                    subnodes.Add(new BinInterpTreeItem
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
                    subnodes.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} ME1/ME2 Unknown1: {me12unknownend1}",
                        Name = "_" + offset,

                        Tag = NodeType.StructLeafName
                    });
                    offset += 4;

                    int me12unknownend2 = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset:X5} ME1/ME2 Unknown2: {me12unknownend2}",
                        Name = "_" + offset,

                        Tag = NodeType.StructLeafName
                    });
                    offset += 4;
                }

                int defaultsClassLink = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Class Defaults: {defaultsClassLink} ({CurrentLoadedExport.FileRef.GetEntryString(defaultsClassLink)}))",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
                {
                    int functionsTableCount = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new BinInterpTreeItem
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
                        (subnodes.Last() as BinInterpTreeItem).Items.Add(new BinInterpTreeItem
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
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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

                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset - 8:X5} Components Table ({CurrentLoadedExport.FileRef.getNameEntry(componentTableNameIndex)})",
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
                    (subnodes.Last() as BinInterpTreeItem).Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset - 12:X5}  {CurrentLoadedExport.FileRef.getNameEntry(nameTableIndex)}({objectName})",
                        Name = "_" + (offset - 12),

                        Tag = NodeType.StructLeafName
                    });
                }
            }
            else
            {
                int componentTableCount = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
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
                    (subnodes.Last() as BinInterpTreeItem).Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{offset - 8:X5}  {CurrentLoadedExport.FileRef.getNameEntry(nameTableIndex)}({objName})",
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

                subnodes.Add(new BinInterpTreeItem
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
                    BinInterpTreeItem subnode = new BinInterpTreeItem
                    {
                        Header = $"0x{offset - 12:X5}  {interfaceIndex} {objectName}",
                        Name = "_" + (offset - 4),
                        Tag = NodeType.StructLeafName
                    };
                    ((BinInterpTreeItem)subnodes.Last()).Items.Add(subnode);

                    //propertypointer
                    interfaceIndex = BitConverter.ToInt32(data, offset);
                    offset += 4;

                    objectName = CurrentLoadedExport.FileRef.GetEntryString(interfaceIndex);
                    subnode.Items.Add(new BinInterpTreeItem
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
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset - 8:X5} Implemented Interfaces Table Count: {interfaceCount} ({CurrentLoadedExport.FileRef.getNameEntry(interfaceTableName)})",
                    Name = "_" + (offset - 8),

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;
                for (int i = 0; i < interfaceCount; i++)
                {
                    int interfaceNameIndex = BitConverter.ToInt32(data, offset);
                    offset += 8;

                    BinInterpTreeItem subnode = new BinInterpTreeItem
                    {
                        Header = $"0x{offset - 8:X5}  {CurrentLoadedExport.FileRef.getNameEntry(interfaceNameIndex)}",
                        Name = "_" + (offset - 8),

                        Tag = NodeType.StructLeafName
                    };
                    ((BinInterpTreeItem)subnodes.Last()).Items.Add(subnode);

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
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Unreal Unique Index: {unrealExportIndex}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int noneUnrealProperty = BitConverter.ToInt32(data, offset);
                //int noneUnrealPropertyIndex = BitConverter.ToInt32(data, offset + 4);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Unreal property None Name: {CurrentLoadedExport.FileRef.getNameEntry(noneUnrealProperty)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafName
                });
                offset += 8;

                int superclassIndex = BitConverter.ToInt32(data, offset);
                string superclassStr = CurrentLoadedExport.FileRef.GetEntryString(superclassIndex);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} Superclass: {superclassIndex}({superclassStr})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{offset:X5} NextItemCompilingChain: {classObjTree} {CurrentLoadedExport.FileRef.GetEntryString(classObjTree)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                if (CurrentLoadedExport.ClassName == "Enum")
                {

                    int enumSize = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new BinInterpTreeItem
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
                        subnodes.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} EnumName[{i}]: {CurrentLoadedExport.FileRef.getNameEntry(enumName)}",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafName
                        });
                        offset += 8;
                    }
                }

                if (CurrentLoadedExport.ClassName == "Const")
                {
                    int literalStringLength = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new BinInterpTreeItem
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
                        subnodes.Add(new BinInterpTreeItem
                        {
                            Header = $"0x{offset:X5} Const Literal Value: {str}",
                            Name = "_" + offset,
                            Tag = NodeType.StrProperty
                        });
                    }
                    else
                    {
                        string str = Gibbed.IO.StreamHelpers.ReadString(stream, (literalStringLength), false, Encoding.ASCII);
                        subnodes.Add(new BinInterpTreeItem
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
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} count: {count}",
                    Name = "_" + pos,

                });
                pos += 4;
                for (int i = 0; i < count && pos < data.Length; i++)
                {
                    int nameRef = BitConverter.ToInt32(data, pos);
                    int nameIdx = BitConverter.ToInt32(data, pos + 4);
                    Guid guid = new Guid(data.Skip(pos + 8).Take(16).ToArray());
                    subnodes.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} {CurrentLoadedExport.FileRef.getNameEntry(nameRef)}_{nameIdx}: {{{guid}}}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafName
                    });
                    //Debug.WriteLine($"{pos:X4} {CurrentLoadedExport.FileRef.getNameEntry(nameRef)}_{nameIdx}: {{{guid}}}");
                    pos += 24;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Self: {entryRefString(bin)}"));
                int actorsCount;
                BinInterpTreeItem levelActorsNode;
                subnodes.Add(levelActorsNode = new BinInterpTreeItem(bin.Position, $"Level Actors: ({actorsCount = bin.ReadInt32()})", NodeType.StructLeafInt)
                {
                    ArrayAddAlgoritm = BinInterpTreeItem.ArrayPropertyChildAddAlgorithm.LevelItem,
                    IsExpanded = true
                });
                levelActorsNode.Items = ReadList(actorsCount, i => new BinInterpTreeItem(bin.Position, $"{i}: {entryRefString(bin)}", NodeType.ArrayLeafObject)
                {
                    ArrayAddAlgoritm = BinInterpTreeItem.ArrayPropertyChildAddAlgorithm.LevelItem,
                    Parent = levelActorsNode,
                });

                subnodes.Add(new BinInterpTreeItem(bin.Position, "URL")
                {
                    Items =
                    {
                        MakeStringNode(bin, "Protocol"),
                        MakeStringNode(bin, "Host"),
                        MakeStringNode(bin, "Map"),
                        MakeStringNode(bin, "Portal"),
                        new BinInterpTreeItem(bin.Position, $"Op: ({bin.ReadInt32()} items)")
                        {
                            Items = ReadList(bin.Skip(-4).ReadInt32(), i => MakeStringNode(bin, $"{i}"))
                        },
                        new BinInterpTreeItem(bin.Position, $"Port: {bin.ReadInt32()}"),
                        new BinInterpTreeItem(bin.Position, $"Valid: {bin.ReadInt32()}")
                    }
                });
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"Model: {entryRefString(bin)}"));
                int modelcomponentsCount;
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"ModelComponents: ({modelcomponentsCount = bin.ReadInt32()})")
                {
                    Items = ReadList(modelcomponentsCount, i => new BinInterpTreeItem(bin.Position, $"{i}: {entryRefString(bin)}"))
                });
                int sequencesCount;
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"GameSequences: ({sequencesCount = bin.ReadInt32()})")
                {
                    Items = ReadList(sequencesCount, i => new BinInterpTreeItem(bin.Position, $"{i}: {entryRefString(bin)}"))
                });
                int texToInstCount;
                int streamableTexInstCount;
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"TextureToInstancesMap: ({texToInstCount = bin.ReadInt32()})")
                {
                    Items = ReadList(texToInstCount, i =>
                                         new BinInterpTreeItem(bin.Position, $"{entryRefString(bin)}: ({streamableTexInstCount = bin.ReadInt32()} StreamableTextureInstances)")
                    {
                        Items = ReadList(streamableTexInstCount, j => new BinInterpTreeItem(bin.Position, $"{j}")
                        {
                            IsExpanded = true,
                            Items =
                            {
                                new BinInterpTreeItem(bin.Position, "BoundingSphere")
                                {
                                    IsExpanded = true,
                                    Items =
                                    {
                                        MakeVectorNode(bin, "Center"),
                                        new BinInterpTreeItem(bin.Position, $"Radius: {bin.ReadSingle()}")
                                    }
                                },
                                new BinInterpTreeItem(bin.Position, $"TexelFactor: {bin.ReadSingle()}")
                            }
                        })
                    })
                });

                if (Pcc.Game == MEGame.ME3)
                {
                    int apexSize;
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"APEX Size: {apexSize = bin.ReadInt32()}"));
                    //should always be zero, but just in case...
                    if (apexSize > 0)
                    {
                        subnodes.Add(new BinInterpTreeItem(bin.Position, $"APEX mesh?: {apexSize} bytes") { Length = apexSize});
                        bin.Skip(apexSize);
                    }
                }

                int cachedPhysBSPDataSize;
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"size of byte: {bin.ReadInt32()}"));
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"CachedPhysBSPData Size: {cachedPhysBSPDataSize = bin.ReadInt32()}"));
                if (cachedPhysBSPDataSize > 0)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"CachedPhysBSPData: {cachedPhysBSPDataSize} bytes") { Length = cachedPhysBSPDataSize });
                    bin.Skip(cachedPhysBSPDataSize);
                }

                int cachedPhysSMDataMapCount;
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"CachedPhysSMDataMap: ({cachedPhysSMDataMapCount = bin.ReadInt32()})")
                {
                    Items = ReadList(cachedPhysSMDataMapCount, i => new BinInterpTreeItem(bin.Position, $"{entryRefString(bin)}")
                    {
                        Items =
                        {
                            MakeVectorNode(bin, "Scale3D"),
                            new BinInterpTreeItem(bin.Position, $"CachedDataIndex: {bin.ReadInt32()}")
                        }
                    })
                });

                int cachedPhysSMDataStoreCount;
                int cachedConvexElementsCount;
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"CachedPhysSMDataStore: ({cachedPhysSMDataStoreCount = bin.ReadInt32()})")
                {
                    Items = ReadList(cachedPhysSMDataStoreCount, i => new BinInterpTreeItem(bin.Position, $"{i}: CachedConvexElements ({cachedConvexElementsCount = bin.ReadInt32()})")
                    {
                        Items = ReadList(cachedConvexElementsCount, j =>
                        {
                            int size;
                            var item = new BinInterpTreeItem(bin.Position, $"{j}: ConvexElementData (size of byte: {bin.ReadInt32()}) (number of bytes: {size = bin.ReadInt32()})")
                            {
                                Length = size + 8
                            };
                            bin.Skip(size);
                            return item;
                        })
                    })
                });

                int cachedPhysPerTriSMDataMapCount;
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"CachedPhysPerTriSMDataMap: ({cachedPhysPerTriSMDataMapCount = bin.ReadInt32()})")
                {
                    Items = ReadList(cachedPhysPerTriSMDataMapCount, i => new BinInterpTreeItem(bin.Position, $"{entryRefString(bin)}")
                    {
                        Items =
                        {
                            MakeVectorNode(bin, "Scale3D"),
                            new BinInterpTreeItem(bin.Position, $"CachedDataIndex: {bin.ReadInt32()}")
                        }
                    })
                });

                int cachedPhysPerTriSMDataStoreCount;
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"CachedPhysPerTriSMDataStore: ({cachedPhysPerTriSMDataStoreCount = bin.ReadInt32()})")
                {
                    Items = ReadList(cachedPhysPerTriSMDataStoreCount, j =>
                    {
                        int size;
                        var item = new BinInterpTreeItem(bin.Position, $"{j}: CachedPerTriData (size of byte: {bin.ReadInt32()}) (number of bytes: {size = bin.ReadInt32()})")
                        {
                            Length = size + 8
                        };
                        bin.Skip(size);
                        return item;
                    })
                });

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"CachedPhysBSPDataVersion: {bin.ReadInt32()}"));
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"CachedPhysBSPDataVersion: {bin.ReadInt32()}"));

                int forceStreamTexturesCount;
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"ForceStreamTextures: ({forceStreamTexturesCount = bin.ReadInt32()})")
                {
                    Items = ReadList(forceStreamTexturesCount, i => new BinInterpTreeItem(bin.Position, $"Texture: {entryRefString(bin)} | ForceStream: {bin.ReadBoolInt()}"))
                });

                subnodes.Add(new BinInterpTreeItem(bin.Position, $"NavListStart: {entryRefString(bin)}"));
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"NavListEnd: {entryRefString(bin)}"));
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"CoverListStart: {entryRefString(bin)}"));
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"CoverListEnd: {entryRefString(bin)}"));
                if (Pcc.Game == MEGame.ME3)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"PylonListStart: {entryRefString(bin)}"));
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"PylonListEnd: {entryRefString(bin)}"));

                    int guidToIntMapCount;
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"guidToIntMap?: ({guidToIntMapCount = bin.ReadInt32()})")
                    {
                        Items = ReadList(guidToIntMapCount, i => new BinInterpTreeItem(bin.Position, $"{bin.ReadValueGuid()}: {bin.ReadInt32()}"))
                    });

                    int coverListCount;
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Coverlinks: ({coverListCount = bin.ReadInt32()})")
                    {
                        Items = ReadList(coverListCount, i => new BinInterpTreeItem(bin.Position, $"{i}: {entryRefString(bin)}"))
                    });

                    int intToByteMapCount;
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"IntToByteMap?: ({intToByteMapCount = bin.ReadInt32()})")
                    {
                        Items = ReadList(intToByteMapCount, i => new BinInterpTreeItem(bin.Position, $"{bin.ReadInt32()}: {bin.ReadByte()}"))
                    });

                    int guidToIntMap2Count;
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"2nd guidToIntMap?: ({guidToIntMap2Count = bin.ReadInt32()})")
                    {
                        Items = ReadList(guidToIntMap2Count, i => new BinInterpTreeItem(bin.Position, $"{bin.ReadValueGuid()}: {bin.ReadInt32()}"))
                    });

                    int navListCount;
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"NavPoints?: ({navListCount = bin.ReadInt32()})")
                    {
                        Items = ReadList(navListCount, i => new BinInterpTreeItem(bin.Position, $"{i}: {entryRefString(bin)}"))
                    });

                    int numbersCount;
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"Ints?: ({numbersCount = bin.ReadInt32()})")
                    {
                        Items = ReadList(numbersCount, i => new BinInterpTreeItem(bin.Position, $"{i}: {bin.ReadInt32()}"))
                    });
                }

                int crossLevelActorsCount;
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"CrossLevelActors?: ({crossLevelActorsCount = bin.ReadInt32()})")
                {
                    Items = ReadList(crossLevelActorsCount, i => new BinInterpTreeItem(bin.Position, $"{i}: {entryRefString(bin)}"))
                });

                if (Pcc.Game == MEGame.ME1)
                {
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"BioArtPlaceable 1?: {entryRefString(bin)}"));
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"BioArtPlaceable 2?: {entryRefString(bin)}"));
                }

                if (Pcc.Game == MEGame.ME3)
                {
                    bool bInitialized;
                    int samplesCount;
                    subnodes.Add(new BinInterpTreeItem(bin.Position, "PrecomputedLightVolume")
                    {
                        Items =
                        {
                            new BinInterpTreeItem(bin.Position, $"bInitialized: ({bInitialized = bin.ReadBoolInt()})"),
                            InitializerHelper.ConditionalAdd(bInitialized, () => new ITreeItem[]
                            {
                                new BinInterpTreeItem(bin.Position, "Bounds")
                                {
                                    IsExpanded = true,
                                    Items =
                                    {
                                        MakeVectorNode(bin, "Min"),
                                        MakeVectorNode(bin, "Max"),
                                        new BinInterpTreeItem(bin.Position, $"IsValid: {bin.ReadBoolByte()}")
                                    }
                                },
                                new BinInterpTreeItem(bin.Position, $"SampleSpacing: {bin.ReadSingle()}"),
                                new BinInterpTreeItem(bin.Position, $"Samples ({samplesCount = bin.ReadInt32()})")
                                {
                                    Items = ReadList(samplesCount, i => new BinInterpTreeItem(bin.Position, $"{i}")
                                    {
                                        Items =
                                        {
                                            MakeVectorNode(bin, "Position"),
                                            new BinInterpTreeItem(bin.Position, $"Radius: {bin.ReadSingle()}"),
                                            //SirCxyrtyx: This is a color, but is serialized as an FQuantizedSHVectorRGB, a vector of colored, quantized spherical harmonic coefficients.
                                            //Conversion to ARGB is possible, but devilishly tricky. Let me know if this is something that's actually needed
                                            new BinInterpTreeItem(bin.Position, $"Ambient Radiance? : {bin.ReadToBuffer(39)}"){ Length = 39}
                                        }
                                    })
                                }

                            })
                        }
                    });
                }

                binarystart = (int) bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private static BinInterpTreeItem MakeVectorNode(MemoryStream bin, string name)
        {
            return new BinInterpTreeItem(bin.Position, $"{name}: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()}, Z: {bin.ReadSingle()})")
            {
                Length = 12
            };
        }

        private List<ITreeItem> StartMaterialScan(byte[] data, ref int binarystart)
        {
            var nodes = new List<ITreeItem>();

            if (binarystart >= data.Length)
            {
                return new List<ITreeItem> { new BinInterpTreeItem { Header = "No Binary Data" } };
            }
            try
            {
                {
                    var subnodes = new List<ITreeItem>();

                    nodes.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{binarystart:X4} Material Resource",
                        Name = "_" + binarystart,
                        Items = subnodes
                    });

                    binarystart = ReadMaterialResource(data, subnodes, binarystart);
                }
                {
                    var subnodes = new List<ITreeItem>();

                    nodes.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{binarystart:X4} Legacy ShaderMap2 Resource",
                        Name = "_" + binarystart,
                        Items = subnodes
                    });

                    binarystart = ReadMaterialResource(data, subnodes, binarystart);
                }
            }
            catch (Exception ex)
            {
                nodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
            }
            return nodes;
        }

        private List<ITreeItem> StartMaterialInstanceConstantScan(byte[] data, ref int binarystart)
        {
            var nodes = new List<ITreeItem>();

            if (binarystart >= data.Length)
            {
                return new List<ITreeItem> { new BinInterpTreeItem { Header = "No Binary Data" } };
            }
            try
            {
                {
                    var subnodes = new List<ITreeItem>();

                    nodes.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{binarystart:X4} Static Permutation Resource",
                        Name = "_" + binarystart,
                        Items = subnodes
                    });

                    binarystart = ReadMaterialResource(data, subnodes, binarystart);

                    binarystart = ReadFStaticParameterSet(data, subnodes, binarystart);
                }
                {
                    var subnodes = new List<ITreeItem>();

                    nodes.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{binarystart:X4} Legacy ShaderMap2 Resource",
                        Name = "_" + binarystart,
                        Items = subnodes
                    });

                    binarystart = ReadMaterialResource(data, subnodes, binarystart);

                    binarystart = ReadFStaticParameterSet(data, subnodes, binarystart);
                }

                if (binarystart != data.Length)
                {
                    //Debugger.Break();
                }
            }
            catch (Exception ex)
            {
                nodes.Add(new BinInterpTreeItem { Header = $"Error reading binary data: {ex}" });
            }
            return nodes;
        }
        private List<ITreeItem> ReadFStaticParameterSetStream(MemoryStream bin)
        {
            var nodes = new List<ITreeItem>();

            nodes.Add(new BinInterpTreeItem(bin.Position, $"Base Material GUID {bin.ReadValueGuid()}") { Length = 16 });
            int staticSwitchParameterCount = bin.ReadInt32();
            var staticSwitchParamsNode = new BinInterpTreeItem(bin.Position - 4, $"Static Switch Parameters, {staticSwitchParameterCount} items") { Length = 4 };

            nodes.Add(staticSwitchParamsNode);
            for (int j = 0; j < staticSwitchParameterCount; j++)
            {
                var paramName = bin.ReadNameReference(Pcc);
                var paramVal = bin.ReadBooleanInt();
                var paramOverride = bin.ReadBooleanInt();
                Guid g = bin.ReadValueGuid();
                staticSwitchParamsNode.Items.Add(new BinInterpTreeItem(bin.Position - 32, $"{j}: Name: {paramName.InstancedString}, Value: {paramVal}, Override: {paramOverride}\nGUID:{g}")
                {
                    Length = 32
                });
            }

            int staticComponentMaskParameterCount = bin.ReadInt32();
            var staticComponentMaskParametersNode = new BinInterpTreeItem(bin.Position - 4, $"Static Component Mask Parameters, {staticComponentMaskParameterCount} items")
            {
                Length = 4
            };
            nodes.Add(staticComponentMaskParametersNode);
            for (int i = 0; i < staticComponentMaskParameterCount; i++)
            {
                var subnodes = new List<ITreeItem>();
                staticComponentMaskParametersNode.Items.Add(new BinInterpTreeItem(bin.Position, $"Parameter {i}")
                {
                    Length = 44,
                    Items = subnodes
                });
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"ParameterName: {bin.ReadNameReference(Pcc).InstancedString}") { Length = 8 });
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"R: {bin.ReadBooleanInt()}") { Length = 4 });
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"G: {bin.ReadBooleanInt()}") { Length = 4 });
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"B: {bin.ReadBooleanInt()}") { Length = 4 });
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"A: {bin.ReadBooleanInt()}") { Length = 4 });
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"bOverride: {bin.ReadBooleanInt()}") { Length = 4 });
                subnodes.Add(new BinInterpTreeItem(bin.Position, $"ExpressionGUID: {bin.ReadValueGuid()}") { Length = 16 });
            }

            if (Pcc.Game == MEGame.ME3)
            {
                int NormalParameterCount = bin.ReadInt32();
                var NormalParametersNode = new BinInterpTreeItem(bin.Position - 4, $"Normal Parameters, {NormalParameterCount} items")
                {
                    Length = 4
                };
                nodes.Add(NormalParametersNode);
                for (int i = 0; i < NormalParameterCount; i++)
                {
                    var subnodes = new List<ITreeItem>();
                    NormalParametersNode.Items.Add(new BinInterpTreeItem(bin.Position, $"Parameter {i}")
                    {
                        Length = 29,
                        Items = subnodes
                    });
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"ParameterName: {bin.ReadNameReference(Pcc).InstancedString}") { Length = 8 });
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"CompressionSettings: {(TextureCompressionSettings)bin.ReadByte()}") { Length = 1 });
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"bOverride: {bin.ReadBooleanInt()}") { Length = 4 });
                    subnodes.Add(new BinInterpTreeItem(bin.Position, $"ExpressionGUID: {bin.ReadValueGuid()}") { Length = 16 });
                }
            }

            return nodes;
        }

        private int ReadFStaticParameterSet(byte[] data, List<ITreeItem> nodes, int binarypos)
        {

            nodes.Add(MakeGuidNode(data, ref binarypos, "Base Material GUID"));
            int staticSwitchParameterCount = BitConverter.ToInt32(data, binarypos);
            var staticSwitchParametersNode = new BinInterpTreeItem
            {
                Header = $"0x{binarypos:X8} : Static Switch Parameters, {staticSwitchParameterCount} items",
                Name = "_" + binarypos,
                Tag = NodeType.Unknown,
                Length = 4
            };
            binarypos += 4;
            nodes.Add(staticSwitchParametersNode);
            for (int j = 0; j < staticSwitchParameterCount; j++)
            {
                var parameterName = ReadNameReference(data, binarypos);
                binarypos += 8;
                var paramVal = BitConverter.ToBoolean(data, binarypos);
                binarypos += 4;
                var paramOverride = BitConverter.ToBoolean(data, binarypos);
                binarypos += 4;
                var expressionGUID = new byte[16];
                Buffer.BlockCopy(data, binarypos, expressionGUID, 0, 16);
                Guid g = new Guid(expressionGUID);
                binarypos += 16;
                staticSwitchParametersNode.Items.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos - 32:X8} : {j}: Name: {parameterName.InstancedString}, Value: {paramVal}, Override: {paramOverride}\nGUID:{g}",
                    Name = $"_{binarypos - 32}",
                    Tag = NodeType.Unknown,
                    Length = 32
                });
            }

            int staticComponentMaskParameterCount = BitConverter.ToInt32(data, binarypos);
            var staticComponentMaskParametersNode = new BinInterpTreeItem
            {
                Header = $"0x{binarypos:X8} : Static Component Mask Parameters, {staticComponentMaskParameterCount} items",
                Name = "_" + binarypos,
                Tag = NodeType.Unknown,
                Length = 4
            };
            binarypos += 4;
            nodes.Add(staticComponentMaskParametersNode);
            for (int i = 0; i < staticComponentMaskParameterCount; i++)
            {
                var subnodes = new List<ITreeItem>();
                staticComponentMaskParametersNode.Items.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X8} : Parameter {i}",
                    Name = "_" + binarypos,
                    Tag = NodeType.Unknown,
                    Length = 44,
                    Items = subnodes
                });
                var parameterName = ReadNameReference(data, binarypos);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X8} : ParameterName: {parameterName.InstancedString}",
                    Name = "_" + binarypos,
                    Tag = NodeType.StructLeafName,
                    Length = 8
                });
                binarypos += 8;
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X8} : R: {BitConverter.ToBoolean(data, binarypos)}",
                    Name = "_" + binarypos,
                    Tag = NodeType.StructLeafBool,
                    Length = 4,
                });
                binarypos += 4;
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X8} : G: {BitConverter.ToBoolean(data, binarypos)}",
                    Name = "_" + binarypos,
                    Tag = NodeType.StructLeafBool,
                    Length = 4,
                });
                binarypos += 4;
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X8} : B: {BitConverter.ToBoolean(data, binarypos)}",
                    Name = "_" + binarypos,
                    Tag = NodeType.StructLeafBool,
                    Length = 4,
                });
                binarypos += 4;
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X8} : A: {BitConverter.ToBoolean(data, binarypos)}",
                    Name = "_" + binarypos,
                    Tag = NodeType.StructLeafBool,
                    Length = 4,
                });
                binarypos += 4;
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X8} : bOverride: {BitConverter.ToBoolean(data, binarypos)}",
                    Name = "_" + binarypos,
                    Tag = NodeType.StructLeafBool,
                    Length = 4,
                });
                binarypos += 4;
                subnodes.Add(MakeGuidNode(data, ref binarypos, "ExpressionGUID"));
            }
            int NormalParameterCount = BitConverter.ToInt32(data, binarypos);
            var NormalParametersNode = new BinInterpTreeItem
            {
                Header = $"0x{binarypos:X8} : Normal Parameters, {NormalParameterCount} items",
                Name = "_" + binarypos,
                Tag = NodeType.Unknown,
                Length = 4
            };
            binarypos += 4;
            nodes.Add(NormalParametersNode);
            for (int i = 0; i < NormalParameterCount; i++)
            {
                var subnodes = new List<ITreeItem>();
                NormalParametersNode.Items.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X8} : Parameter {i}",
                    Name = "_" + binarypos,
                    Tag = NodeType.Unknown,
                    Length = 29,
                    Items = subnodes
                });
                var parameterName = ReadNameReference(data, binarypos);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X8} : ParameterName: {parameterName.InstancedString}",
                    Name = "_" + binarypos,
                    Tag = NodeType.StructLeafName,
                    Length = 8
                });
                binarypos += 8;
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X8} : CompressionSettings: {(Unreal.ME3Enums.TextureCompressionSettings)data[binarypos]}",
                    Name = "_" + binarypos,
                    Tag = NodeType.StructLeafByte,
                    Length = 1
                });
                binarypos += 1;
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X8} : bOverride: {BitConverter.ToBoolean(data, binarypos)}",
                    Name = "_" + binarypos,
                    Tag = NodeType.StructLeafBool,
                    Length = 4
                });
                binarypos += 4;
                subnodes.Add(MakeGuidNode(data, ref binarypos, "ExpressionGUID"));
            }

            return binarypos;
        }

        private NameReference ReadNameReference(byte[] data, int binarypos)
        {
            return new NameReference(Pcc.getNameEntry(BitConverter.ToInt32(data, binarypos)), BitConverter.ToInt32(data, binarypos + 4));
        }

        private int ReadMaterialResource(byte[] data, List<ITreeItem> subnodes, int binarypos)
        {
            int compileErrorsCount = BitConverter.ToInt32(data, binarypos);
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"0x{binarypos:X4} Compile Error Count: {BitConverter.ToInt32(data, binarypos)}",
                Name = "_" + binarypos
            });
            binarypos += 4;
            for (int i = 0; i < compileErrorsCount; i++)
            {
                int strLen = -2 * BitConverter.ToInt32(data, binarypos);
                binarypos += 4;
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X4} Compile Error {i}: {Encoding.Unicode.GetString(data, binarypos, strLen)}",
                    Name = "_" + binarypos,
                    Length = strLen
                });
                binarypos += strLen;
            }

            if (CurrentLoadedExport.FileRef.Game != MEGame.ME2)
            {
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X4} Zero: {BitConverter.ToInt32(data, binarypos)}",
                    Name = "_" + binarypos
                });
                binarypos += 4;
            }

            int maxTextureDependencyLength = BitConverter.ToInt32(data, binarypos);
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"0x{binarypos:X4} Max Texture Dependency Length: {maxTextureDependencyLength} (Unused?)",
                Name = "_" + binarypos
            });
            binarypos += 4;

            subnodes.Add(MakeGuidNode(data, ref binarypos, "GUID"));

            uint numUserTexCoords = BitConverter.ToUInt32(data, binarypos);
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"0x{binarypos:X4} Number of User Texture Coordinates: {numUserTexCoords}",
                Name = "_" + binarypos
            });
            binarypos += 4;
            int textureCount = BitConverter.ToInt32(data, binarypos);
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"0x{binarypos:X4} Uniform Expression Texture Count: {textureCount}",
                Name = "_" + binarypos
            });
            binarypos += 4;

            while (binarypos <= data.Length - 4 && textureCount > 0)
            {
                int val = BitConverter.ToInt32(data, binarypos);
                string name = val.ToString();

                if (val > 0 && val <= CurrentLoadedExport.FileRef.Exports.Count)
                {
                    ExportEntry exp = CurrentLoadedExport.FileRef.Exports[val - 1];
                    name += $" {exp.PackageFullName}.{exp.ObjectName} ({exp.ClassName})";
                }
                else if (val < 0 && Math.Abs(val) <= CurrentLoadedExport.FileRef.Imports.Count)
                {
                    int csImportVal = Math.Abs(val) - 1;
                    ImportEntry imp = CurrentLoadedExport.FileRef.Imports[csImportVal];
                    name += $" {imp.PackageFullName}.{imp.ObjectName} ({imp.ClassName})";
                }

                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X4} {name}",
                    Tag = NodeType.StructLeafObject,
                    Name = "_" + binarypos
                });
                binarypos += 4;
                textureCount--;
            }

            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"0x{binarypos:X4} bUsesSceneColor: {BitConverter.ToBoolean(data, binarypos)}",
                Name = "_" + binarypos
            });
            binarypos += 4;
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"0x{binarypos:X4} bUsesSceneDepth: {BitConverter.ToBoolean(data, binarypos)}",
                Name = "_" + binarypos
            });
            binarypos += 4;
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"0x{binarypos:X4} bUsesDynamicParameter: {BitConverter.ToBoolean(data, binarypos)}",
                Name = "_" + binarypos
            });
            binarypos += 4;
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"0x{binarypos:X4} bUsesLightmapUVs: {BitConverter.ToBoolean(data, binarypos)}",
                Name = "_" + binarypos
            });
            binarypos += 4;
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"0x{binarypos:X4} bUsesMaterialVertexPositionOffset: {BitConverter.ToBoolean(data, binarypos)}",
                Name = "_" + binarypos
            });
            binarypos += 4;
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"0x{binarypos:X4} Unknown: {BitConverter.ToInt32(data, binarypos)}",
                Name = "_" + binarypos
            });
            binarypos += 4;
            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"0x{binarypos:X4} UsingTransforms: {(ECoordTransformUsage)BitConverter.ToUInt32(data, binarypos)}",
                Name = "_" + binarypos
            });
            binarypos += 4;

            int textureLookupCount = BitConverter.ToInt32(data, binarypos);
            var textureLookups = new BinInterpTreeItem
            {
                Header = $"0x{binarypos:X4} Texture Lookup Count: {textureLookupCount}",
                Name = "_" + binarypos,
            };
            subnodes.Add(textureLookups);
            binarypos += 4;
            for (int j = 0; j < textureLookupCount; j++)
            {
                textureLookups.Items.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X4} Texture Coordinate Index: {BitConverter.ToInt32(data, binarypos)}",
                    Name = "_" + binarypos
                });
                binarypos += 4;
                textureLookups.Items.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X4} Texture Index: {BitConverter.ToInt32(data, binarypos)} (index into Uniform Expression Texture array)",
                    Name = "_" + binarypos
                });
                binarypos += 4;
                textureLookups.Items.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X4} UScale: {BitConverter.ToSingle(data, binarypos)}",
                    Name = "_" + binarypos
                });
                binarypos += 4;
                textureLookups.Items.Add(new BinInterpTreeItem
                {
                    Header = $"0x{binarypos:X4} VScale: {BitConverter.ToSingle(data, binarypos)}",
                    Name = "_" + binarypos
                });
                binarypos += 4;
            }

            subnodes.Add(new BinInterpTreeItem
            {
                Header = $"0x{binarypos:X4} Zero: {BitConverter.ToInt32(data, binarypos)}",
                Name = "_" + binarypos
            });
            binarypos += 4;
            return binarypos;
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
            if ((CurrentLoadedExport.Header[0x1f] & 0x2) == 0)
            {
                return subnodes;
            }

            try
            {
                int pos = binarystart;
                int count = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} Count: {count}",
                    Name = "_" + pos

                });
                pos += 4;
                while (pos + 8 <= data.Length && count > 0)
                {
                    var exportRef = BitConverter.ToInt32(data, pos);
                    subnodes.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4}: {exportRef} Prefab: {CurrentLoadedExport.FileRef.getEntry(exportRef).GetFullPath}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafObject
                    });
                    pos += 4;
                    exportRef = BitConverter.ToInt32(data, pos);
                    if (exportRef == 0)
                    {
                        (subnodes.Last() as BinInterpTreeItem).Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4}: {exportRef} Level Object: Null",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafObject
                        });
                    }
                    else
                    {
                        (subnodes.Last() as BinInterpTreeItem).Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4}: {exportRef} Level Object: {CurrentLoadedExport.FileRef.getEntry(exportRef).GetFullPath}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafObject
                        });
                    }

                    pos += 4;
                    count--;
                }

            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<ITreeItem> StartSkeletalMeshScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            var game = CurrentLoadedExport.FileRef.Game;
            try
            {
                int pos = binarystart;
                pos += 28; //bounding
                int count = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} Material Count: {count}",
                    Name = "_" + pos,

                });
                pos += 4;
                for (int i = 0; i < count; i++)
                {
                    int material = BitConverter.ToInt32(data, pos);
                    subnodes.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Material: ({material}) {CurrentLoadedExport.FileRef.getEntry(material)?.GetFullPath ?? ""}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafObject
                    });
                    pos += 4;
                }
                // SKELMESH TREE
                pos = binarystart;  //reset to  start again
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $" --------- FULL TREE ----------",
                    Name = "_" + pos,

                    Tag = NodeType.Unknown
                });

                var BoundingBox = new BinInterpTreeItem
                {
                    Header = $"0x{pos:X4} Boundings Box",
                    Name = "_" + pos,
                    Tag = NodeType.Unknown
                };
                subnodes.Add(BoundingBox);
                //Get Origin X, Y, Z
                float boxoriginX = BitConverter.ToSingle(data, pos);
                pos += 4;
                float boxoriginY = BitConverter.ToSingle(data, pos);
                pos += 4;
                float boxoriginZ = BitConverter.ToSingle(data, pos);
                pos += 4;
                BoundingBox.Items.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} Origin: X:({boxoriginX}) Y:({boxoriginY}) Z:({boxoriginZ})",
                    Name = "_" + pos,

                    Tag = NodeType.Unknown
                });


                //Get Size X, Y, Z
                float sizeX = BitConverter.ToSingle(data, pos);
                pos += 4;
                float sizeY = BitConverter.ToSingle(data, pos);
                pos += 4;
                float sizeZ = BitConverter.ToSingle(data, pos);
                pos += 4;
                BoundingBox.Items.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} Size: X:({sizeX}) Y:({sizeY}) Z:({sizeZ})",
                    Name = "_" + pos,

                    Tag = NodeType.Unknown
                });

                //Get Radius R
                float radius = BitConverter.ToSingle(data, pos);
                pos += 4;
                BoundingBox.Items.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} Radius: R:({radius}) ",
                    Name = "_" + pos,

                    Tag = NodeType.Unknown
                });

                //Materials (again)
                var materials = new BinInterpTreeItem
                {
                    Header = $"{pos:X4} Materials: {count}",
                    Name = "_" + pos,
                    Tag = NodeType.StructLeafInt
                };
                subnodes.Add(materials);
                pos += 4;
                for (int m = 0; m < count; m++)
                {
                    int material = BitConverter.ToInt32(data, pos);
                    materials.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Material: ({material}) {CurrentLoadedExport.FileRef.getEntry(material)?.GetFullPath ?? ""}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafObject
                    });
                    pos += 4;
                }

                //Origin and Rotation
                var skmLocation = new BinInterpTreeItem
                {
                    Header = $"0x{pos:X4} Origin and Rotation",
                    Name = "_" + pos,
                    Tag = NodeType.Unknown
                };
                subnodes.Add(skmLocation);
                //Get Origin X, Y, Z
                float originX = BitConverter.ToInt32(data, pos);
                pos += 4;
                float originY = BitConverter.ToInt32(data, pos);
                pos += 4;
                float originZ = BitConverter.ToInt32(data, pos);
                pos += 4;
                skmLocation.Items.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} Origin: X:({originX}) Y:({originY}) Z:({originZ})",
                    Name = "_" + pos,

                    Tag = NodeType.Unknown
                });

                //Get Rotation X, Y, Z ?CONVERT TO RADIANS/DEG?
                float rotX = BitConverter.ToInt32(data, pos);
                pos += 4;
                float rotY = BitConverter.ToInt32(data, pos);
                pos += 4;
                float rotZ = BitConverter.ToInt32(data, pos);
                pos += 4;
                skmLocation.Items.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} Rotation: X:({rotX}) Y:({rotY}) Z:({rotZ})",
                    Name = "_" + pos,

                    Tag = NodeType.Unknown
                });

                //Bone Data
                int bCount = BitConverter.ToInt32(data, pos);
                var bones = new BinInterpTreeItem
                {
                    Header = $"{pos:X4} Bones: {bCount}",
                    Name = "_" + pos,
                    Tag = NodeType.StructLeafInt
                };
                subnodes.Add(bones);
                pos += 4;
                for (int b = 0; b < bCount; b++)
                {
                    int nBone = BitConverter.ToInt32(data, pos);
                    pos += 4;
                    int nBoneidx = BitConverter.ToInt32(data, pos);
                    pos -= 4; //reset to start for leaf
                    var nBoneNode = new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Bone {b}: ({nBone}) {CurrentLoadedExport.FileRef.getNameEntry(nBone)} _ {nBoneidx}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafName
                    };
                    bones.Items.Add(nBoneNode);
                    pos += 8;

                    int unk1 = BitConverter.ToInt32(data, pos);
                    nBoneNode.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Unknown1: {unk1}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt
                    });
                    pos += 4;

                    float orientX = BitConverter.ToSingle(data, pos);
                    nBoneNode.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Orientation: X: {orientX}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafFloat
                    });
                    pos += 4;

                    float orientY = BitConverter.ToSingle(data, pos);
                    nBoneNode.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Y: {orientY}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafFloat
                    });
                    pos += 4;

                    float orientZ = BitConverter.ToSingle(data, pos);
                    nBoneNode.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Z: {orientZ}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafFloat
                    });
                    pos += 4;

                    float orientW = BitConverter.ToSingle(data, pos);
                    nBoneNode.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} W: {orientW}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafFloat
                    });
                    pos += 4;

                    float posX = BitConverter.ToSingle(data, pos);
                    nBoneNode.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Position: X: {posX}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafFloat
                    });
                    pos += 4;

                    float posY = BitConverter.ToSingle(data, pos);
                    nBoneNode.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Y: {posY}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafFloat
                    });
                    pos += 4;

                    float posZ = BitConverter.ToSingle(data, pos);
                    nBoneNode.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Z: {posZ}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafFloat
                    });
                    pos += 4;

                    int nChildren = BitConverter.ToInt32(data, pos);
                    nBoneNode.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Children: {nChildren}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt
                    });
                    pos += 4;

                    int bnParent = BitConverter.ToInt32(data, pos);
                    nBoneNode.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Parent Bone: {bnParent}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt
                    });
                    pos += 4;

                    if (game == MEGame.ME3 || game == MEGame.UDK) //Color in ME3 and UDK only
                    {
                        int bnColor = BitConverter.ToInt32(data, pos);
                        nBoneNode.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Color: {bnColor}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt
                        });
                        pos += 4;
                    }
                }
                int bnDepth = BitConverter.ToInt32(data, pos);
                bones.Items.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} Bone Depth: {bnDepth}",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;

                //LOD DATA
                int lodCount = BitConverter.ToInt32(data, pos);
                var lods = new BinInterpTreeItem
                {
                    Header = $"{pos:X4} Levels of Detail (LODs): {lodCount}",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                };
                subnodes.Add(lods);
                pos += 4;

                for (int lod = 0; lod < lodCount; lod++)
                {
                    var nLOD = new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} LOD {lod}",
                        Name = "_" + pos,

                        Tag = NodeType.Unknown
                    };
                    lods.Items.Add(nLOD);

                    int sectionCt = BitConverter.ToInt32(data, pos); // Sections
                    var sections = new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Sections: {sectionCt}",
                        Name = "_" + pos,

                        Tag = NodeType.Unknown
                    };
                    nLOD.Items.Add(sections);
                    pos += 4;

                    for (int sc = 0; sc < sectionCt; sc++)
                    {
                        var nSection = new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Section: {sc}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        };
                        sections.Items.Add(nSection);

                        if (game == MEGame.UDK)  //UDK section quite different
                        {
                            int mat = BitConverter.ToInt16(data, pos);
                            nSection.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"{pos:X4} Material Index: {mat}",
                                Name = "_" + pos,

                                Tag = NodeType.StructLeafInt,
                            });
                            pos += 2;

                            int chunk = BitConverter.ToInt16(data, pos);
                            nSection.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"{pos:X4} Chunk Index: {chunk}",
                                Name = "_" + pos,

                                Tag = NodeType.StructLeafInt,
                            });
                            pos += 2;

                            int baseidx = BitConverter.ToInt32(data, pos);
                            nSection.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"{pos:X4} Base Index: {baseidx}",
                                Name = "_" + pos,

                                Tag = NodeType.StructLeafInt,
                            });
                            pos += 4;

                            int nTriangles = BitConverter.ToInt16(data, pos); //Section Triangles
                            nSection.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"{pos:X4} Triangles: {nTriangles}",
                                Name = "_" + pos,

                                Tag = NodeType.StructLeafInt,
                            });
                            pos += 4;

                            bool bSortTri = BitConverter.ToBoolean(data, pos);
                            nSection.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"{pos:X4} Sort Triangles: {bSortTri}",
                                Name = "_" + pos,

                                Tag = NodeType.StructLeafBool,
                            });
                            pos += 1;
                        }
                        else
                        {
                            int chunk = BitConverter.ToInt32(data, pos);
                            nSection.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"{pos:X4} Chunk Index: {chunk}",
                                Name = "_" + pos,

                                Tag = NodeType.StructLeafInt,
                            });
                            pos += 4;

                            int baseidx = BitConverter.ToInt32(data, pos);
                            nSection.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"{pos:X4} Base Index: {baseidx}",
                                Name = "_" + pos,

                                Tag = NodeType.StructLeafInt,
                            });
                            pos += 4;

                            var initpos = pos;  //Section Triangles
                            int nTriangles = 0;
                            if (game == MEGame.ME3)
                            {
                                nTriangles = BitConverter.ToInt32(data, pos);  //ME3 int32
                                pos += 4;
                            }
                            else
                            {
                                nTriangles = BitConverter.ToInt16(data, pos); //ME2/1 int16
                                pos += 2;
                            }
                            nSection.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"{initpos:X4} Triangles: {nTriangles}",
                                Name = "_" + initpos,

                                Tag = NodeType.StructLeafInt,
                            });
                        }
                    }

                    var idxHeader = new BinInterpTreeItem // Multi-size index container
                    {
                        Header = $"{pos:X4} Multi-size index container",
                        Name = "_" + pos,

                        Tag = NodeType.Unknown
                    };
                    nLOD.Items.Add(idxHeader);

                    if (game == MEGame.UDK)
                    {
                        int iCPU = BitConverter.ToInt32(data, pos);
                        idxHeader.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Needs CPU Access: {iCPU}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        });
                        pos += 4;

                        byte dataType = data[pos]; //Single byte
                        idxHeader.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Datatype Size: {dataType}",
                            Name = "_" + pos,

                            Tag = NodeType.Unknown,
                        });
                        pos += 1;

                    }

                    int idxSize = BitConverter.ToInt32(data, pos); // Index Size
                    idxHeader.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} IndexSize: {idxSize}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    });
                    pos += 4;

                    int countIdx = BitConverter.ToInt32(data, pos); // Index count
                    var indexes = new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Indexes: {countIdx}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    };
                    idxHeader.Items.Add(indexes);
                    pos += 4;

                    for (int ic = 0; ic < countIdx; ic++)
                    {
                        int nIndex = BitConverter.ToInt16(data, pos);  //Index size = 2 (so int16)
                        indexes.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} {ic} : {nIndex}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        });
                        pos += 2;
                    }

                    if (game != MEGame.UDK)
                    {
                        int Unknown1 = BitConverter.ToInt32(data, pos); // Unknown 1 not UDK
                        var UnkList1 = new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Unknown 1 List: {Unknown1}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        };
                        idxHeader.Items.Add(UnkList1);
                        pos += 4;

                        for (int uk1 = 0; uk1 < Unknown1; uk1++)
                        {
                            int ukIndex = BitConverter.ToInt16(data, pos);  //int16 unknown
                            UnkList1.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"{pos:X4} {uk1} : {ukIndex}",
                                Name = "_" + pos,

                                Tag = NodeType.StructLeafInt,
                            });
                            pos += 2;
                        }
                    }

                    int nActBones = BitConverter.ToInt32(data, pos); // Active Bones
                    var ActBones = new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Active Bones: {nActBones}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    };
                    nLOD.Items.Add(ActBones);
                    pos += 4;

                    for (int ab = 0; ab < nActBones; ab++)
                    {
                        int abIndex = BitConverter.ToInt16(data, pos);  // int16 = me3
                        ActBones.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} {ab} : {abIndex}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        });
                        pos += 2;
                    }

                    if (game != MEGame.UDK)
                    {
                        int Unknown2 = BitConverter.ToInt32(data, pos); // Unknown 2 Not in UDK
                        var UnkList2 = new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Unknown 2 Bool List: {Unknown2}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        };
                        nLOD.Items.Add(UnkList2);
                        pos += 4;

                        for (int uk2 = 0; uk2 < Unknown2; uk2++)
                        {
                            bool uk2Bool = BitConverter.ToBoolean(data, pos);  //Bool unknown
                            UnkList2.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"{pos:X4} {uk2} : {uk2Bool}",
                                Name = "_" + pos,

                                Tag = NodeType.StructLeafBool,
                            });
                            pos += 1;
                        }
                    }

                    // Chunk Data
                    int chunkCt = BitConverter.ToInt32(data, pos);
                    var chunks = new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Chunks: {chunkCt}",
                        Name = "_" + pos,

                        Tag = NodeType.Unknown
                    };
                    nLOD.Items.Add(chunks);
                    pos += 4;

                    for (int cc = 0; cc < chunkCt; cc++) // Chunks
                    {
                        var nChunk = new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Chunk: {cc}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        };
                        chunks.Items.Add(nChunk);

                        int basevertexidx = BitConverter.ToInt32(data, pos);
                        nChunk.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Base Vertex Index: {basevertexidx}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        });
                        pos += 4;

                        int rigidskinVertex = BitConverter.ToInt32(data, pos); //Rigid vertices collection
                        var rigidvertices = new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Rigid Skin Vertices: {rigidskinVertex}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        };
                        nChunk.Items.Add(rigidvertices);
                        pos += 4;

                        for (int rv = 0; rv < rigidskinVertex; rv++)
                        {
                            //UDK has lots of unused values
                            int rvpos = pos;
                            float vPosX = BitConverter.ToSingle(data, rvpos);
                            rvpos += 4;
                            float vPosY = BitConverter.ToSingle(data, rvpos);
                            rvpos += 4;
                            float vPosZ = BitConverter.ToSingle(data, rvpos);
                            rvpos += 4;
                            float TanX = BitConverter.ToSingle(data, rvpos);
                            rvpos += 4;
                            float TanY = BitConverter.ToSingle(data, rvpos);
                            rvpos += 4;
                            float TanZ = BitConverter.ToSingle(data, rvpos);
                            rvpos += 4;
                            float uv1U = BitConverter.ToSingle(data, rvpos);
                            rvpos += 4;
                            float uv1V = BitConverter.ToSingle(data, rvpos);
                            rvpos += 4; //32

                            if (game == MEGame.UDK)
                            {
                                float uv2U = BitConverter.ToSingle(data, rvpos);
                                rvpos += 4;
                                float uv2V = BitConverter.ToSingle(data, rvpos);
                                rvpos += 4;
                                float uv3U = BitConverter.ToSingle(data, rvpos);
                                rvpos += 4;
                                float uv3V = BitConverter.ToSingle(data, rvpos);
                                rvpos += 4; //48
                                float uv4U = BitConverter.ToSingle(data, rvpos);
                                rvpos += 4;
                                float uv4V = BitConverter.ToSingle(data, rvpos);
                                rvpos += 4; //56
                                int color = BitConverter.ToInt32(data, rvpos);
                                rvpos += 4; //60

                                byte bone = data[rvpos];  //SINGLE BYTE
                                rvpos += 1;

                                rigidvertices.Items.Add(new BinInterpTreeItem
                                {
                                    Header = $"{pos:X4} {rv}: Position: X:{vPosX} Y:{vPosY} Z:{vPosZ} Tangent X:{TanX} Y:{TanY} Z:{TanZ} UV(0) U:{uv1U} W:{uv1U} UV(1) U:{uv2U} W:{uv2U} UV(2) U:{uv3U} W:{uv3U} UV(3) U:{uv4U} W:{uv4U} Color: {color} Bone: {bone}",
                                    Name = "_" + pos,

                                    Tag = NodeType.Unknown,
                                });
                                pos += 61;
                            }
                            else
                            {

                                byte bone = data[rvpos];  //SINGLE BYTE
                                rvpos += 1;

                                rigidvertices.Items.Add(new BinInterpTreeItem
                                {
                                    Header = $"{pos:X4} {rv}: Position: X:{vPosX} Y:{vPosY} Z:{vPosZ} Tangent X:{TanX} Y:{TanY} Z:{TanZ}  UV U:{uv1U} W:{uv1U} Bone:{bone}",
                                    Name = "_" + pos,

                                    Tag = NodeType.Unknown,
                                });
                                pos += 33;
                            }
                        }

                        int softskinVertex = BitConverter.ToInt32(data, pos); //Soft vertices collection
                        var softvertices = new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Soft Skin Vertices: {softskinVertex}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        };
                        nChunk.Items.Add(softvertices);
                        pos += 4;

                        for (int sv = 0; sv < softskinVertex; sv++)
                        {
                            //UDK has lots of unused values
                            int rvpos = pos;
                            float vPosX = BitConverter.ToSingle(data, rvpos);
                            rvpos += 4;
                            float vPosY = BitConverter.ToSingle(data, rvpos);
                            rvpos += 4;
                            float vPosZ = BitConverter.ToSingle(data, rvpos);
                            rvpos += 4;
                            float TanX = BitConverter.ToSingle(data, rvpos);
                            rvpos += 4;
                            float TanY = BitConverter.ToSingle(data, rvpos);
                            rvpos += 4;
                            float TanZ = BitConverter.ToSingle(data, rvpos);
                            rvpos += 4;
                            float uv1U = BitConverter.ToSingle(data, rvpos);
                            rvpos += 4;
                            float uv1V = BitConverter.ToSingle(data, rvpos);
                            rvpos += 4; //32

                            if (game == MEGame.UDK)
                            {
                                float uv2U = BitConverter.ToSingle(data, rvpos);
                                rvpos += 4;
                                float uv2V = BitConverter.ToSingle(data, rvpos);
                                rvpos += 4;
                                float uv3U = BitConverter.ToSingle(data, rvpos);
                                rvpos += 4;
                                float uv3V = BitConverter.ToSingle(data, rvpos);
                                rvpos += 4;
                                float uv4U = BitConverter.ToSingle(data, rvpos);
                                rvpos += 4;
                                float uv4V = BitConverter.ToSingle(data, rvpos);
                                rvpos += 4;
                                int color = BitConverter.ToInt32(data, rvpos);
                                rvpos += 4;

                                //Influence Bones
                                byte inflbnA = data[rvpos];  //SINGLE BYTE Influence
                                rvpos += 1;
                                byte inflbnB = data[rvpos];
                                rvpos += 1;
                                byte inflbnC = data[rvpos];
                                rvpos += 1;
                                byte inflbnD = data[rvpos];
                                rvpos += 1;

                                //Influence Weights
                                byte inflwA = data[rvpos];  //SINGLE BYTE Influence
                                rvpos += 1;
                                byte inflwB = data[rvpos];
                                rvpos += 1;
                                byte inflwC = data[rvpos];
                                rvpos += 1;
                                byte inflwD = data[rvpos];
                                rvpos += 1;

                                softvertices.Items.Add(new BinInterpTreeItem
                                {
                                    Header = $"{pos:X4} {sv}: Position: X:{vPosX} Y:{vPosY} Z:{vPosZ} Tangent X:{TanX} Y:{TanY} Z:{TanZ} UV(0) U:{uv1U} W:{uv1U} UV(1) U:{uv2U} W:{uv2U} UV(2) U:{uv3U} W:{uv3U} UV(3) U:{uv4U} W:{uv4U} Color: {color} Influence bones: ({inflbnA}, {inflbnB}, {inflbnC}, {inflbnD}) Influence Weights: ({inflwA}, {inflwB}, {inflwC}, {inflwD})",
                                    Name = "_" + pos,

                                    Tag = NodeType.Unknown,
                                });
                                pos += 68;
                            }
                            else
                            {
                                //Influence Bones
                                byte inflbnA = data[rvpos];  //SINGLE BYTE Influence
                                rvpos += 1;
                                byte inflbnB = data[rvpos];
                                rvpos += 1;
                                byte inflbnC = data[rvpos];
                                rvpos += 1;
                                byte inflbnD = data[rvpos];
                                rvpos += 1;

                                //Influence Weights
                                byte inflwA = data[rvpos];
                                rvpos += 1;
                                byte inflwB = data[rvpos];
                                rvpos += 1;
                                byte inflwC = data[rvpos];
                                rvpos += 1;
                                byte inflwD = data[rvpos];
                                rvpos += 1;

                                softvertices.Items.Add(new BinInterpTreeItem
                                {
                                    Header = $"{pos:X4} {sv}: Position: X:{vPosX} Y:{vPosY} Z:{vPosZ} Tangent X:{TanX} Y:{TanY} Z:{TanZ}  UV U:{uv1U} W:{uv1U} Influence bones: ({inflbnA}, {inflbnB}, {inflbnC}, {inflbnD}) Influence Weights: ({inflwA}, {inflwB}, {inflwC}, {inflwD})",
                                    Name = "_" + pos,

                                    Tag = NodeType.Unknown,
                                });
                                pos += 40;
                            }
                        }

                        int nMapBones = BitConverter.ToInt32(data, pos); // Bone Map
                        var mapBones = new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Bones Map: {nMapBones}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        };
                        nChunk.Items.Add(mapBones);
                        pos += 4;

                        for (int mb = 0; mb < nMapBones; mb++)
                        {
                            int mbIndex = BitConverter.ToInt16(data, pos);  // int16 = me3
                            mapBones.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"{pos:X4} {mb} : {mbIndex}",
                                Name = "_" + pos,

                                Tag = NodeType.StructLeafInt,
                            });
                            pos += 2;
                        }

                        int numRigidVertex = BitConverter.ToInt32(data, pos);
                        nChunk.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Num Rigid Vertices: {numRigidVertex}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        });
                        pos += 4;

                        int numSoftVertex = BitConverter.ToInt32(data, pos);
                        nChunk.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Num Soft Vertices: {numSoftVertex}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        });
                        pos += 4;

                        int maxBoneInfluence = BitConverter.ToInt32(data, pos);
                        nChunk.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Max Bone Influence: {maxBoneInfluence}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        });
                        pos += 4;
                    }

                    int Size1 = BitConverter.ToInt32(data, pos); // Size
                    nLOD.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Size: {Size1}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    });
                    pos += 4;

                    int nVertices = BitConverter.ToInt32(data, pos); // NumVertices
                    nLOD.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} NumVertices: {nVertices}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    });
                    pos += 4;

                    if (game != MEGame.UDK)
                    {
                        int Unknown3 = BitConverter.ToInt32(data, pos); // Unknown 3 Not in UDK
                        var UnkList3 = new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Unknown List 3: {Unknown3}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        };
                        nLOD.Items.Add(UnkList3);
                        pos += 4;

                        for (int uk3 = 0; uk3 < Unknown3; uk3++)
                        {
                            int uk3IndexA = BitConverter.ToInt32(data, pos);  //int32 unknown
                            pos += 4;
                            int uk3IndexB = BitConverter.ToInt32(data, pos);  //int32 unknown
                            pos += 4;
                            int uk3IndexC = BitConverter.ToInt32(data, pos);  //int32 unknown
                            pos += 4;
                            int uk3IndexD = BitConverter.ToInt32(data, pos);  //int32 unknown
                            pos += 4;
                            UnkList3.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"{pos:X4} {uk3} : unkA {uk3IndexA} unkB {uk3IndexB} unkC {uk3IndexC} unkD {uk3IndexD}",
                                Name = "_" + pos,

                                Tag = NodeType.Unknown,
                            });
                        }
                    }

                    int nReqBones = BitConverter.ToInt32(data, pos); //Required Bones
                    var reqBones = new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Required Bones: {nReqBones}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    };
                    nLOD.Items.Add(reqBones);
                    pos += 4;

                    for (int rq = 0; rq < nReqBones; rq++)
                    {
                        //single byte integers (max 256 bones)
                        reqBones.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} {rq} : {data[pos]}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafByte,
                        });
                        pos += 1;
                    }

                    //Raw Point Data
                    int rawPoint1 = BitConverter.ToInt32(data, pos);
                    nLOD.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Raw Point Indices Flag: {rawPoint1}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    });
                    pos += 4;

                    int rawPoint2 = BitConverter.ToInt32(data, pos);
                    nLOD.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Raw Point Indices Count: {rawPoint2}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    });
                    pos += 4;

                    int rawPoint3 = BitConverter.ToInt32(data, pos);
                    nLOD.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Raw Point Indices Size: {rawPoint3}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    });
                    pos += 4;

                    int rawPoint4 = BitConverter.ToInt32(data, pos);
                    nLOD.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Raw Point Indices Offset: {rawPoint4}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    });
                    pos += 4;

                    if (rawPoint3 != 0)
                    {
                        var rawpoints = new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Raw Point Indices Collection",
                            Name = "_" + pos,

                            Tag = NodeType.Unknown,
                        };
                        nLOD.Items.Add(rawpoints);
                        int rapos = pos;
                        for (int rp = 0; rp < rawPoint3; rp++)
                        {
                            rawpoints.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"{rapos:X4} {rp} : {data[rapos]:X2}",
                                Name = "_" + rapos,

                                Tag = NodeType.StructLeafByte,
                            });
                            rapos += 1;
                        }
                        pos += rawPoint3;
                    }

                    if (game != MEGame.ME1)
                    {
                        int Unknown9 = BitConverter.ToInt32(data, pos); // Unknown 9 ME3 or ME2 or UDK (Not ME1)
                        nLOD.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Unknown 9: {Unknown9}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        });
                        pos += 4;
                    }

                    var vtxGPUskin = new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Vertex Buffer GPU Skin",
                        Name = "_" + pos,

                        Tag = NodeType.Unknown
                    };
                    nLOD.Items.Add(vtxGPUskin);

                    if (game == MEGame.ME3 || game == MEGame.UDK)
                    {
                        int nTexCoord = BitConverter.ToInt32(data, pos);
                        var lTexCoord = new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Texture Coordinates: {nTexCoord}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        };
                        vtxGPUskin.Items.Add(lTexCoord);
                        pos += 4;

                        if (game == MEGame.UDK)
                        {
                            int useFullUV = BitConverter.ToInt32(data, pos); // UDK only
                            vtxGPUskin.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"{pos:X4} Use Full Precision UVs: {useFullUV}",
                                Name = "_" + pos,

                                Tag = NodeType.StructLeafInt,
                            });
                            pos += 4;

                            int usePackedPre = BitConverter.ToInt32(data, pos); // UDK only
                            vtxGPUskin.Items.Add(new BinInterpTreeItem
                            {
                                Header = $"{pos:X4} Use Packed Precision: {usePackedPre}",
                                Name = "_" + pos,

                                Tag = NodeType.StructLeafInt,
                            });
                            pos += 4;
                        }

                        //Get Extension X, Y, Z
                        float vetxX = BitConverter.ToSingle(data, pos);
                        pos += 4;
                        float vetxY = BitConverter.ToSingle(data, pos);
                        pos += 4;
                        float vetxZ = BitConverter.ToSingle(data, pos);
                        pos += 4;
                        vtxGPUskin.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Extension: X:({vetxX}) Y:({vetxY}) Z:({vetxZ})",
                            Name = "_" + pos,

                            Tag = NodeType.Unknown
                        });

                        //Get origin X, Y, Z
                        float vorgX = BitConverter.ToSingle(data, pos);
                        pos += 4;
                        float vorgY = BitConverter.ToSingle(data, pos);
                        pos += 4;
                        float vorgZ = BitConverter.ToSingle(data, pos);
                        pos += 4;
                        vtxGPUskin.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Origin: X:({vorgX}) Y:({vorgY}) Z:({vorgZ})",
                            Name = "_" + pos,

                            Tag = NodeType.Unknown
                        });
                    }

                    int vtxSize = BitConverter.ToInt32(data, pos);
                    vtxGPUskin.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Vertex Size: {vtxSize}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    });
                    pos += 4;


                    int nVertex = BitConverter.ToInt32(data, pos); //Vertex Count
                    var vertices = new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Vertices: {nVertex}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    };
                    vtxGPUskin.Items.Add(vertices);
                    pos += 4;

                    for (int v = 0; v < nVertex; v++)
                    {
                        int vpos = pos; //use seperate positioning as vertex size is variable
                        float TanX = BitConverter.ToSingle(data, vpos);
                        vpos += 4;
                        float TanY = BitConverter.ToSingle(data, vpos);
                        vpos += 4;
                        float TanZ = BitConverter.ToSingle(data, vpos);
                        vpos += 4;
                        float vPosX = BitConverter.ToSingle(data, vpos);
                        vpos += 4;
                        float vPosY = BitConverter.ToSingle(data, vpos);
                        vpos += 4;
                        float vPosZ = BitConverter.ToSingle(data, vpos);
                        vpos += 4;
                        int infB1 = BitConverter.ToInt16(data, vpos);
                        vpos += 2;
                        int infW1 = BitConverter.ToInt16(data, vpos);
                        vpos += 2;
                        int infB2 = BitConverter.ToInt16(data, vpos);
                        vpos += 2;
                        int infW2 = BitConverter.ToInt16(data, vpos);
                        vpos += 2;
                        int infB3 = BitConverter.ToInt16(data, vpos);
                        vpos += 2;
                        int infW3 = BitConverter.ToInt16(data, vpos);
                        vpos += 2;
                        int infB4 = BitConverter.ToInt16(data, vpos);
                        vpos += 2;
                        int infW4 = BitConverter.ToInt16(data, vpos);
                        vpos += 2;
                        float uvU = BitConverter.ToSingle(data, vpos);
                        vpos += 4;
                        float uvV = BitConverter.ToSingle(data, vpos);
                        vpos += 4;
                        vertices.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} {v}: Tangent X:{TanX} Y:{TanY} Z:{TanZ} Position: X:{vPosX} Y:{vPosY} Z:{vPosZ} Influences: {infB1}:{infW1} {infB2}:{infW2} {infB3}:{infW4} {infB4}:{infW4} UV U:{uvU} W:{uvV}",
                            Name = "_" + pos,

                            Tag = NodeType.Unknown,
                        });
                        pos += vtxSize;
                    }

                    if (game == MEGame.ME3 || game == MEGame.UDK)
                    {
                        int Unknown4 = BitConverter.ToInt32(data, pos); // Unknown 4 appears ME3/UDK (not ME2/ME1)
                        nLOD.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Unknown (Index GPU buffer size?): {Unknown4}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        });
                        pos += 4;

                        if (Unknown4 > 0)
                        {

                            int Unk4Count = BitConverter.ToInt32(data, pos);
                            var unk4List = new BinInterpTreeItem
                            {
                                Header = $"{pos:X4} ?Index GPU buffer?: {Unk4Count}",
                                Name = "_" + pos,

                                Tag = NodeType.StructLeafInt,
                            };
                            nLOD.Items.Add(unk4List);
                            pos += 4;

                            for (int uk4 = 0; uk4 < Unk4Count; uk4++)
                            {
                                int nUnk4 = BitConverter.ToInt32(data, pos);
                                unk4List.Items.Add(new BinInterpTreeItem
                                {
                                    Header = $"{pos:X4} {uk4} : {nUnk4}",
                                    Name = "_" + pos,

                                    Tag = NodeType.StructLeafInt,
                                });
                                pos += Unknown4;
                            }
                        }
                    }

                    if (game == MEGame.UDK) //UDK only - are these LOD or tail?
                    {
                        int nUnkU1 = BitConverter.ToInt32(data, pos);
                        nLOD.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Unknown UDK 1 : {nUnkU1}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        });
                        pos += 4;

                        int nUnkU2 = BitConverter.ToInt32(data, pos);
                        nLOD.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Unknown UDK 2 : {nUnkU2}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        });
                        pos += 4;

                        int nUnkU3 = BitConverter.ToInt32(data, pos);
                        nLOD.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Unknown UDK 3 : {nUnkU3}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        });
                        pos += 4;

                        int nUnkU4 = BitConverter.ToInt32(data, pos);
                        nLOD.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Unknown UDK 4 : {nUnkU4}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        });
                        pos += 4;

                        byte nUnkU5 = data[pos];
                        nLOD.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Unknown UDK 5 : {nUnkU5}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafByte,
                        });
                        pos += 1;
                    }
                }

                var tail = new BinInterpTreeItem  // Tail
                {
                    Header = $"{pos:X4} Tail",
                    Name = "_" + pos,

                    Tag = NodeType.Unknown
                };
                subnodes.Add(tail);

                int blCount = BitConverter.ToInt32(data, pos);
                var bonelist = new BinInterpTreeItem
                {
                    Header = $"{pos:X4} Bone List: {blCount}",
                    Name = "_" + pos,
                    Tag = NodeType.StructLeafInt
                };
                tail.Items.Add(bonelist);
                pos += 4;
                for (int bl = 0; bl < blCount; bl++)
                {
                    int bnName = BitConverter.ToInt32(data, pos);
                    pos += 4;
                    int bnIdx = BitConverter.ToInt32(data, pos);
                    pos += 4;
                    int iBone = BitConverter.ToInt32(data, pos);
                    pos -= 8;
                    bonelist.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} {bl}: {CurrentLoadedExport.FileRef.getNameEntry(bnName)}_{bnIdx}  Nbr: {iBone}",
                        Name = "_" + pos,
                        Tag = NodeType.StructLeafName
                    });
                    pos += 12;
                }

                int Unknown5 = BitConverter.ToInt32(data, pos); // Unknown ME3/ME2/ME1/UDK
                tail.Items.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} Unknown 5: {Unknown5}",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt,
                });
                pos += 4;

                if (game == MEGame.ME3 || game == MEGame.UDK)
                {
                    int Unknown6 = BitConverter.ToInt32(data, pos); // Unknown ME3/UDK (not ME2/1)
                    tail.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Unknown 6: {Unknown6}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    });
                    pos += 4;

                    if (game == MEGame.UDK)
                    {
                        int nUnkU6 = BitConverter.ToInt32(data, pos);
                        tail.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Unknown UDK 6 : {nUnkU6}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        });
                        pos += 4;
                    }

                    int Unknown7 = BitConverter.ToInt32(data, pos); // Unknown 7 ME3/UDK (not ME2/1)
                    var lUnknown7 = new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Unknown List 7: {Unknown7}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    };
                    tail.Items.Add(lUnknown7);
                    pos += 4;

                    for (int uk7 = 0; uk7 < Unknown7; uk7++)
                    {
                        int Unknown8 = BitConverter.ToInt32(data, pos); // Unknown list
                        lUnknown7.Items.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} {uk7} : {Unknown8}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafInt,
                        });
                        pos += 4;
                    }
                }

                if (game == MEGame.UDK)  // Extended Tail in UDK
                {

                    int nUnkU7 = BitConverter.ToInt32(data, pos);
                    tail.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Unknown UDK 7 : {nUnkU7}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    });
                    pos += 4;

                    int nUnkU8 = BitConverter.ToInt32(data, pos);
                    tail.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Unknown UDK 8 : {nUnkU8}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    });
                    pos += 4;

                    int nUnkU9 = BitConverter.ToInt32(data, pos);
                    tail.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Unknown UDK 9 : {nUnkU9}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    });
                    pos += 4;

                    int nUnkU10 = BitConverter.ToInt32(data, pos);
                    tail.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Unknown UDK 10 : {nUnkU10}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    });
                    pos += 4;

                    int nUnkU11 = BitConverter.ToInt32(data, pos);
                    tail.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Unknown UDK 11 : {nUnkU11}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    });
                    pos += 4;

                    int nUnkU12 = BitConverter.ToInt32(data, pos);
                    tail.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} Unknown UDK 12 : {nUnkU12}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafInt,
                    });
                    pos += 4;
                }
                binarystart = pos;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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
                        smacitems.Add(Pcc.getUExport(prop.Value));
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
                    subnodes.Add(new BinInterpTreeItem
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
                    BinInterpTreeItem smcanode = new BinInterpTreeItem
                    {
                        Tag = NodeType.Unknown
                    };
                    ExportEntry associatedData = smacitems[smcaindex];
                    string staticmesh = "";
                    string objtext = "Null - unused data";
                    if (associatedData != null)
                    {
                        objtext = $"[Export {associatedData.UIndex}] {associatedData.ObjectName}_{associatedData.indexValue}";

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
                                staticmesh = CurrentLoadedExport.FileRef.getEntry(staticmeshexp).ObjectName;
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
                        BinInterpTreeItem node = new BinInterpTreeItem
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
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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
                        slcaitems.Add(CurrentLoadedExport.FileRef.getEntry(prop.Value) as ExportEntry);
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
                    subnodes.Add(new BinInterpTreeItem
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
                    BinInterpTreeItem slcanode = new BinInterpTreeItem
                    {
                        Tag = NodeType.Unknown
                    };
                    ExportEntry assossiateddata = slcaitems[slcaindex];
                    string objtext = "Null - unused data";
                    if (assossiateddata != null)
                    {
                        objtext = $"[Export {assossiateddata.UIndex}] {assossiateddata.ObjectName}_{assossiateddata.indexValue}";
                    }

                    slcanode.Header = $"{start:X4} [{slcaindex}] {objtext}";
                    slcanode.Name = "_" + start;
                    subnodes.Add(slcanode);

                    //Read nodes
                    for (int i = 0; i < 16; i++)
                    {
                        float slcadata = BitConverter.ToSingle(data, start);
                        BinInterpTreeItem node = new BinInterpTreeItem
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
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;

        }

        private List<ITreeItem> StartStaticMeshScan(byte[] data, ref int binarystart)
        {
            /*
             *  
             *  Bounding +28
             *  RB_BodySetup <----------------------------
             *  more bounding +28 
             *  size +4 bytes
             *  count +4 bytes
             *  kDOPTree +(size*count)
             *  size +4 bytes
             *  count +4 bytes
             *  RawTris +(size*count)
             *  meshversion +4
             *  lodcount +4
             *      guid +16
             *      sectioncount +4
             *          MATERIAL <------------------------
             *          +36
             *          unk5
             *          +13
             *      section[0].unk5 == 1 ? +12 : +4
             */
            var subnodes = new List<ITreeItem>();
            try
            {
                int pos = binarystart;
                pos += 28;
                int rbRef = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} RB_BodySetup: ({rbRef}) {CurrentLoadedExport.FileRef.getEntry(rbRef)?.GetFullPath ?? ""}",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafObject

                });
                pos += 28; //bounding
                int size = BitConverter.ToInt32(data, pos);
                int count = BitConverter.ToInt32(data, pos + 4);
                pos += 8 + (size * count); //kDOPTree
                size = BitConverter.ToInt32(data, pos);
                count = BitConverter.ToInt32(data, pos + 4);
                pos += 8 + (size * count); //RawTris
                pos += 4; //meshversion
                int lodCount = BitConverter.ToInt32(data, pos);
                pos += 4;
                int unk5 = 0;
                for (int i = 0; i < lodCount; i++)
                {
                    pos += 16; //guid
                    int sectionCount = BitConverter.ToInt32(data, pos);
                    pos += 4;
                    for (int j = 0; j < sectionCount; j++)
                    {
                        int material = BitConverter.ToInt32(data, pos);
                        subnodes.Add(new BinInterpTreeItem
                        {
                            Header = $"{pos:X4} Material: ({material}) {CurrentLoadedExport.FileRef.getEntry(material)?.GetFullPath ?? ""}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafObject
                        });
                        pos += 36;
                        if (j == 0)
                        {
                            unk5 = BitConverter.ToInt32(data, pos);
                        }
                        pos += 13;
                    }
                    pos += unk5 == 1 ? 12 : 4;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<ITreeItem> StartTextureBinaryScan(byte[] data)
        {
            var subnodes = new List<ITreeItem>();

            try
            {
                var textureData = new MemoryStream(data);

                int unrealExportIndex = textureData.ReadInt32();
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"0x{textureData.Position - 4:X4} Unreal Unique Index: {unrealExportIndex}",
                    Name = "_" + (textureData.Position - 4),

                    Tag = NodeType.StructLeafInt
                });

                PropertyCollection properties = CurrentLoadedExport.GetProperties();
                if (textureData.Length == properties.endOffset)
                    return subnodes; // no binary data


                textureData.Position = properties.endOffset;
                if (CurrentLoadedExport.FileRef.Game != MEGame.ME3)
                {
                    textureData.Seek(12, SeekOrigin.Current); // 12 zeros
                    subnodes.Add(new BinInterpTreeItem(textureData.Position, $"File Offset: {textureData.ReadInt32()}"));
                }

                int numMipMaps = textureData.ReadInt32();
                subnodes.Add(new BinInterpTreeItem(textureData.Position - 4, $"Num MipMaps: {numMipMaps}"));
                for (int l = 0; l < numMipMaps; l++)
                {
                    var mipMapNode = new BinInterpTreeItem
                    {
                        Header = $"0x{textureData.Position:X4} MipMap #{l}",
                        Name = "_" + (textureData.Position)

                    };
                    subnodes.Add(mipMapNode);

                    StorageTypes storageType = (StorageTypes)textureData.ReadInt32();
                    mipMapNode.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{textureData.Position - 4:X4} Storage Type: {storageType}",
                        Name = "_" + (textureData.Position - 4)

                    });

                    var uncompressedSize = textureData.ReadInt32();
                    mipMapNode.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{textureData.Position - 4:X4} Uncompressed Size: {uncompressedSize}",
                        Name = "_" + (textureData.Position - 4)

                    });

                    var compressedSize = textureData.ReadInt32();
                    mipMapNode.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{textureData.Position - 4:X4} Compressed Size: {compressedSize}",
                        Name = "_" + (textureData.Position - 4)

                    });

                    var dataOffset = textureData.ReadInt32();
                    mipMapNode.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{textureData.Position - 4:X4} Data Offset: 0x{dataOffset:X8}",
                        Name = "_" + (textureData.Position - 4)

                    });

                    switch (storageType)
                    {
                        case StorageTypes.pccUnc:
                            textureData.Seek(uncompressedSize, SeekOrigin.Current);
                            break;
                        case StorageTypes.pccLZO:
                        case StorageTypes.pccZlib:
                            textureData.Seek(compressedSize, SeekOrigin.Current);
                            break;
                    }

                    var mipWidth = textureData.ReadInt32();
                    mipMapNode.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{textureData.Position - 4:X4} Mip Width: {mipWidth}",
                        Name = "_" + (textureData.Position - 4),
                        Tag = NodeType.StructLeafInt
                    });

                    var mipHeight = textureData.ReadInt32();
                    mipMapNode.Items.Add(new BinInterpTreeItem
                    {
                        Header = $"0x{textureData.Position - 4:X4} Mip Height: {mipHeight}",
                        Name = "_" + (textureData.Position - 4),
                        Tag = NodeType.StructLeafInt
                    });
                }
                textureData.ReadInt32(); //skip
                if (CurrentLoadedExport.FileRef.Game != MEGame.ME1)
                {
                    byte[] textureGuid = Gibbed.IO.StreamHelpers.ReadBytes(textureData, 16);
                    var textureGuidNode = new BinInterpTreeItem
                    {
                        Header = $"0x{textureData.Position - 16:X4} Texture GUID: {new Guid(textureGuid)}",
                        Name = "_" + (textureData.Position - 16)

                    };
                    subnodes.Add(textureGuidNode);
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
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
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} Unknown: {unk1}",
                    Name = "_" + pos,

                });
                pos += 4;
                int length = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} bik length: {length} (0x{length:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                length = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} bik length: {length} (0x{length:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                int offset = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinInterpTreeItem
                {
                    Header = $"{pos:X4} bik offset in file: {offset} (0x{offset:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                if (pos < data.Length && CurrentLoadedExport.GetProperty<NameProperty>("Filename") == null)
                {
                    subnodes.Add(new BinInterpTreeItem
                    {
                        Header = $"{pos:X4} The rest of the binary is the bik.",
                        Name = "_" + pos,

                        Tag = NodeType.Unknown
                    });
                    subnodes.Add(new BinInterpTreeItem
                    {
                        Header = "The stream offset to this data will be automatically updated when this file is saved.",
                        Tag = NodeType.Unknown
                    });
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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
                    Header = $"{pos:X4} bik length: {length} (0x{length:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                length = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{pos:X4} bik length: {length} (0x{length:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                int offset = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{pos:X4} bik offset in file: {offset} (0x{offset:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                if (pos < data.Length && CurrentLoadedExport.GetProperty<NameProperty>("Filename") == null)
                {
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"{pos:X4} The rest of the binary is the bik.",
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
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
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
                    var node = new BinInterpTreeItem();

                    switch (interpreterMode)
                    {
                        case InterpreterMode.Objects:
                            {
                                int val = BitConverter.ToInt32(data, binarypos);
                                string name = $"0x{binarypos:X6}: {val}";
                                if (CurrentLoadedExport.FileRef.isEntry(val) && CurrentLoadedExport.FileRef.getEntry(val) is IEntry ent)
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
                                    nodeText += $"{val.ToString().PadRight(14, ' ')}{CurrentLoadedExport.FileRef.getNameEntry(val)}";
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
                subnodes.Add(new BinInterpTreeItem() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }
    }
}