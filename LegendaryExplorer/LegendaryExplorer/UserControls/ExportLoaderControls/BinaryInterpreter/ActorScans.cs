using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Newtonsoft.Json;
using static LegendaryExplorer.UserControls.ExportLoaderControls.BinaryNodeFactory;

namespace LegendaryExplorer.UserControls.ExportLoaderControls;

public partial class BinaryInterpreterWPF
{
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
                    Offset = start
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
                    var smc_data = associatedData.DataReadOnly;
                    int staticmeshstart = 0x4;
                    bool found = false;
                    while (staticmeshstart < smc_data.Length && smc_data.Length - 8 >= staticmeshstart)
                    {
                        ulong nameindex = EndianReader.ToUInt64(smc_data, staticmeshstart, Pcc.Endian);
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
                        int staticmeshexp = EndianReader.ToInt32(smc_data, staticmeshstart + 0x18, Pcc.Endian);
                        if (staticmeshexp > 0 && staticmeshexp < CurrentLoadedExport.FileRef.ExportCount)
                        {
                            staticmesh = Pcc.GetEntry(staticmeshexp).ObjectName.Instanced;
                        }
                    }
                }

                smcanode.Header = $"{start:X4} [{smcaindex}] {objtext} {staticmesh}";
                smcanode.Offset = start;
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
                        case 0:
                            label = "X1:";
                            break;
                        case 1:
                            label = "X2: X-scaling-Axis: ";
                            break;
                        case 2:
                            label = "X3:";
                            break;
                        case 3:
                            label = "XT:";
                            break;
                        case 4:
                            label = "Y1: Y-scaling axis";
                            break;
                        case 5:
                            label = "Y2:";
                            break;
                        case 6:
                            label = "Y3:";
                            break;
                        case 7:
                            label = "YT:";
                            break;
                        case 8:
                            label = "Z1:";
                            break;
                        case 9:
                            label = "Z2:";
                            break;
                        case 10:
                            label = "Z3: Z-scaling axis";
                            break;
                        case 11:
                            label = "ZT:";
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
                            label = "CameraCollisionDistanceScalar:";
                            break;
                    }

                    node.Header += $" {label} {smcadata}";

                    //TODO: Lookup staticmeshcomponent so we can see what this actually is without changing to the export

                    node.Offset = start;
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

    private List<ITreeItem> StartTerrainScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var materialMapFile = Path.Combine(AppDirectories.ObjectDatabasesFolder, $"{CurrentLoadedExport.Game}MaterialMap.json");
            Dictionary<Guid, string> materialGuidMap = null;
            if (File.Exists(materialMapFile))
            {
                materialGuidMap = JsonConvert.DeserializeObject<Dictionary<Guid, string>>(File.ReadAllText(materialMapFile));
            }
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);

            subnodes.Add(MakeArrayNode(bin, "Heights", i => MakeUInt16Node(bin, $"{i}")));
            subnodes.Add(MakeArrayNode(bin, "InfoData", i => new BinInterpNode(bin.Position, $"{i}: {(EInfoFlags)bin.ReadByte()}")));
            subnodes.Add(MakeArrayNode(bin, "AlphaMaps", i => MakeArrayNode(bin, $"{i}: Data", j => new BinInterpNode(bin.Position, $"{j}: {bin.ReadByte()}"))));
            subnodes.Add(MakeArrayNode(bin, "WeightedTextureMaps", i => MakeEntryNode(bin, $"{i}", Pcc)));
            for (int k = Pcc.Game is MEGame.ME1 or MEGame.UDK ? 1 : 2; k > 0; k--)
            {
                subnodes.Add(MakeArrayNode(bin, "CachedTerrainMaterials", i =>
                {
                    var node = MakeMaterialResourceNode(bin, $"{i}", materialGuidMap);

                    node.Items.Add(MakeEntryNode(bin, "Terrain", Pcc));
                    node.Items.Add(new BinInterpNode(bin.Position, "Mask")
                    {
                        IsExpanded = true,
                        Items =
                        {
                            MakeInt32Node(bin, "NumBits"),
                            new BinInterpNode(bin.Position, $"BitMask: {Convert.ToString(bin.ReadInt64(), 2).PadLeft(64, '0')}")
                        }
                    });
                    node.Items.Add(MakeArrayNode(bin, "MaterialIds", j => MakeMaterialGuidNode(bin, $"{j}", materialGuidMap), true));
                    if (Pcc.Game >= MEGame.ME3)
                    {
                        node.Items.Add(MakeGuidNode(bin, "LightingGuid"));
                    }

                    if (Pcc.Game == MEGame.UDK)
                    {
                        node.Items.Add(MakeBoolIntNode(bin, "bEnableSpecular"));
                    }
                    return node;
                }));
            }
            if (Pcc.Game != MEGame.ME1 && Pcc.Game != MEGame.UDK)
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
    private enum EInfoFlags : byte
    {
        TID_Visibility_Off = 1,
        TID_OrientationFlip = 2,
        TID_Unreachable = 4,
        TID_Locked = 8,
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
                    Offset = start
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
                slcanode.Offset = start;
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

                    node.Offset = start;
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
}