using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    public partial class BinaryInterpreterWPF
    {
        private List<ITreeItem> StartMaterialScan(byte[] data, ref int binarystart)
        {
            var nodes = new List<ITreeItem>();

            if (binarystart >= data.Length)
            {
                return new List<ITreeItem> { new BinInterpNode { Header = "No Binary Data" } };
            }
            try
            {
                var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
                bin.JumpTo(binarystart);
                if (Pcc.Game == MEGame.UDK)
                {
                    nodes.Add(MakeInt32Node(bin, "Number of material resources"));
                }
                nodes.Add(MakeMaterialResourceNode(bin, "Material Resource"));
                if (Pcc.Game != MEGame.UDK)
                {
                    nodes.Add(MakeMaterialResourceNode(bin, "2nd Material Resource"));
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
                var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
                bin.JumpTo(binarystart);

                nodes.Add(MakeMaterialResourceNode(bin, "Material Resource"));
                nodes.Add(ReadFStaticParameterSet(bin));
                nodes.Add(MakeMaterialResourceNode(bin, "2nd Material Resource"));
                nodes.Add(ReadFStaticParameterSet(bin));
            }
            catch (Exception ex)
            {
                nodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }
            return nodes;
        }

        private BinInterpNode ReadFStaticParameterSet(EndianReader bin)
        {
            var nodes = new List<ITreeItem>();
            var result = new BinInterpNode(bin.Position, "StaticParameterSet")
            {
                IsExpanded = true,
                Items = nodes
            };

            try
            {
                nodes.Add(new BinInterpNode(bin.Position, $"Base Material GUID {bin.ReadGuid()}") { Length = 16 });
                int staticSwitchParameterCount = bin.ReadInt32();
                var staticSwitchParamsNode = new BinInterpNode(bin.Position - 4, $"Static Switch Parameters, {staticSwitchParameterCount} items") { Length = 4 };

                nodes.Add(staticSwitchParamsNode);
                for (int j = 0; j < staticSwitchParameterCount; j++)
                {
                    var paramName = bin.ReadNameReference(Pcc);
                    var paramVal = bin.ReadBoolInt();
                    var paramOverride = bin.ReadBoolInt();
                    Guid g = bin.ReadGuid();
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
                    subnodes.Add(new BinInterpNode(bin.Position, $"R: {bin.ReadBoolInt()}") { Length = 4 });
                    subnodes.Add(new BinInterpNode(bin.Position, $"G: {bin.ReadBoolInt()}") { Length = 4 });
                    subnodes.Add(new BinInterpNode(bin.Position, $"B: {bin.ReadBoolInt()}") { Length = 4 });
                    subnodes.Add(new BinInterpNode(bin.Position, $"A: {bin.ReadBoolInt()}") { Length = 4 });
                    subnodes.Add(new BinInterpNode(bin.Position, $"bOverride: {bin.ReadBoolInt()}") { Length = 4 });
                    subnodes.Add(new BinInterpNode(bin.Position, $"ExpressionGUID: {bin.ReadGuid()}") { Length = 16 });
                }

                if (Pcc.Game >= MEGame.ME3)
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
                        subnodes.Add(new BinInterpNode(bin.Position, $"bOverride: {bin.ReadBoolInt()}") { Length = 4 });
                        subnodes.Add(new BinInterpNode(bin.Position, $"ExpressionGUID: {bin.ReadGuid()}") { Length = 16 });
                    }
                }
                if (Pcc.Game == MEGame.UDK)
                {
                    int TerrainWeightParametersCount = bin.ReadInt32();
                    var TerrainWeightParametersNode = new BinInterpNode(bin.Position - 4, $"Terrain Weight Parameters, {TerrainWeightParametersCount} items")
                    {
                        Length = 4
                    };
                    nodes.Add(TerrainWeightParametersNode);
                    for (int i = 0; i < TerrainWeightParametersCount; i++)
                    {
                        var subnodes = new List<ITreeItem>();
                        TerrainWeightParametersNode.Items.Add(new BinInterpNode(bin.Position, $"Parameter {i}")
                        {
                            Length = 32,
                            Items = subnodes
                        });
                        subnodes.Add(new BinInterpNode(bin.Position, $"ParameterName: {bin.ReadNameReference(Pcc).Instanced}") { Length = 8 });
                        subnodes.Add(MakeInt32Node(bin, "WeightmapIndex"));
                        subnodes.Add(new BinInterpNode(bin.Position, $"bOverride: {bin.ReadBoolInt()}") { Length = 4 });
                        subnodes.Add(new BinInterpNode(bin.Position, $"ExpressionGUID: {bin.ReadGuid()}") { Length = 16 });
                    }
                }
            }
            catch (Exception ex)
            {
                nodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }
            return result;
        }

        private BinInterpNode MakeMaterialResourceNode(EndianReader bin, string name, Dictionary<Guid, string> materialGuidMap = null)
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
                nodes.Add(MakeMaterialGuidNode(bin, "ID", materialGuidMap));
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
                    ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game == MEGame.ME3 || Pcc.Game.IsLEGame(), () => MakeBoolIntNode(bin, "unknown bool?"))
                }));
                nodes.Add(new BinInterpNode(bin.Position, $"UsingTransforms: {(ECoordTransformUsage)bin.ReadUInt32()}"));
                if (Pcc.Game == MEGame.ME1)
                {
                    int count = bin.ReadInt32();
                    var unkListNode = new BinInterpNode(bin.Position - 4, $"MaterialUniformExpressions list? ({count} items)");
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
                            ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game.IsLEGame(), () => MakeInt32Node(bin, "Unk"))
                        }
                    }));
                }
                nodes.Add(MakeInt32Node(bin, "DummyDroppedFallbackComponents"));
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

                if (Pcc.Game == MEGame.UDK)
                {
                    nodes.Add(MakeInt32Node(bin, "BlendModeOverrideValue"));
                    nodes.Add(MakeInt32Node(bin, "bIsBlendModeOverrided"));
                    nodes.Add(MakeInt32Node(bin, "bIsMaskedOverrideValue"));
                }
            }
            catch (Exception ex)
            {
                nodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }
            return node;
        }
    }
}
