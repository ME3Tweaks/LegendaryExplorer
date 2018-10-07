using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Be.Windows.Forms;
using Gammtek.Conduit.Extensions;
using Gibbed.IO;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using static ME3Explorer.BinaryInterpreter;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for BinaryInterpreterWPF.xaml
    /// </summary>
    public partial class BinaryInterpreterWPF : ExportLoaderControl
    {
        private HexBox BinaryInterpreter_Hexbox;

        public BinaryInterpreterWPF()
        {
            InitializeComponent();
        }

        static readonly string[] ParsableBinaryClasses = { "Level", "StaticMeshCollectionActor", "StaticLightCollectionActor", "SeekFreeShaderCache", "Class", "BioStage", "ObjectProperty", "Const",
            "Enum", "ArrayProperty","FloatProperty", "IntProperty", "BoolProperty","Enum","ObjectRedirector", "WwiseEvent", "Material", "StaticMesh", "MaterialInstanceConstant",
            "BioDynamicAnimSet", "StaticMeshComponent", "SkeletalMeshComponent", "SkeletalMesh", "PrefabInstance",
            "WwiseStream", "WwiseBank", "TextureMovie", "GuidCache", "World", "Texture2D"};

        public override bool CanParse(IExportEntry exportEntry)
        {
            return ParsableBinaryClasses.Contains(exportEntry.ClassName);
        }

        public override void LoadExport(IExportEntry export)
        {
            CurrentLoadedExport = export;
            DynamicByteProvider db = new DynamicByteProvider(export.Data);
            BinaryInterpreter_Hexbox.ByteProvider = db;

            StartBinaryScan();
        }

        #region static stuff
        public enum nodeType
        {
            Unknown = -1,
            StructProperty = 0,
            IntProperty = 1,
            FloatProperty = 2,
            ObjectProperty = 3,
            NameProperty = 4,
            BoolProperty = 5,
            ByteProperty = 6,
            ArrayProperty = 7,
            StrProperty = 8,
            StringRefProperty = 9,
            DelegateProperty = 10,
            None,
            BioMask4Property,

            ArrayLeafObject,
            ArrayLeafName,
            ArrayLeafEnum,
            ArrayLeafStruct,
            ArrayLeafBool,
            ArrayLeafString,
            ArrayLeafFloat,
            ArrayLeafInt,
            ArrayLeafByte,

            StructLeafByte,
            StructLeafFloat,
            StructLeafDeg, //indicates this is a StructProperty leaf that is in degrees (actually unreal rotation units)
            StructLeafInt,
            StructLeafObject,
            StructLeafName,
            StructLeafBool,
            StructLeafStr,
            StructLeafArray,
            StructLeafEnum,
            StructLeafStruct,

            Root,
        }

        #endregion

        private void StartBinaryScan()
        {
            BinaryInterpreter_TreeView.Items.Clear();
            byte[] data = CurrentLoadedExport.Data;
            int binarystart = CurrentLoadedExport.propsEnd();
            TreeViewItem topLevelTree = new TreeViewItem()
            {
                Header = $"{binarystart:X4} : {CurrentLoadedExport.ObjectName}",
                Tag = nodeType.Root,
                Name = "_0",
                IsExpanded = true
            };
            BinaryInterpreter_TreeView.Items.Add(topLevelTree);

            switch (CurrentLoadedExport.ClassName)
            {
                case "IntProperty":
                case "BoolProperty":
                case "ArrayProperty":
                case "FloatProperty":
                case "ObjectProperty":
                    StartObjectScan(topLevelTree, data, binarystart);
                    break;
                case "BioDynamicAnimSet":
                    StartBioDynamicAnimSetScan(topLevelTree, data, binarystart);
                    break;
                case "ObjectRedirector":
                    StartObjectRedirectorScan(topLevelTree, data, binarystart);
                    break;
                case "WwiseStream":
                case "WwiseBank":
                    Scan_WwiseStreamBank(topLevelTree, data, binarystart);
                    break;
                case "WwiseEvent":
                    Scan_WwiseEvent(topLevelTree, data, binarystart);
                    break;
                case "BioStage":
                    StartBioStageScan(topLevelTree, data, binarystart);
                    break;
                case "Class":
                    StartClassScan(topLevelTree, data, binarystart);
                    break;
                case "Enum":
                case "Const":
                    StartEnumScan(topLevelTree, data, binarystart);
                    break;
                case "GuidCache":
                    StartGuidCacheScan(topLevelTree, data, binarystart);
                    break;
                case "Level":
                    StartLevelScan(topLevelTree, data, binarystart);
                    break;
                case "Material":
                case "MaterialInstanceConstant":
                    StartMaterialScan(topLevelTree, data, binarystart);
                    break;
                case "PrefabInstance":
                    StartPrefabInstanceScan(topLevelTree, data, binarystart);
                    break;
                case "SkeletalMesh":
                    StartSkeletalMeshScan(topLevelTree, data, binarystart);
                    break;
                case "StaticMeshCollectionActor":
                    StartStaticMeshCollectionActorScan(topLevelTree, data, binarystart);
                    break;
                case "StaticMesh":
                    StartStaticMeshScan(topLevelTree, data, binarystart);
                    break;
            }
        }

        #region scans
        private void StartObjectRedirectorScan(TreeViewItem topLevelTree, byte[] data, int binarystart)
        {
            int redirnum = BitConverter.ToInt32(data, binarystart);
            topLevelTree.Items.Add(new TreeViewItem()
            {
                Header = $"{binarystart:X4} Redirect references to this export to: {redirnum} {CurrentLoadedExport.FileRef.getEntry(redirnum).GetFullPath}",
                Name = binarystart.ToString()
            });
        }

        private void StartObjectScan(TreeViewItem topLevelTree, byte[] data, int binarystart)
        {
            try
            {
                int offset = 0; //this property starts at 0 for parsing
                int unrealExportIndex = BitConverter.ToInt32(data, offset);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"0x{offset:X5} Unreal Unique Index: {unrealExportIndex}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int noneUnrealProperty = BitConverter.ToInt32(data, offset);
                int noneUnrealPropertyIndex = BitConverter.ToInt32(data, offset + 4);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"0x{offset:X5} Unreal property None Name: {CurrentLoadedExport.FileRef.getNameEntry(noneUnrealProperty)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafName
                });
                offset += 8;

                int superclassIndex = BitConverter.ToInt32(data, offset);
                string superclassStr = getEntryFullPath(superclassIndex);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"0x{offset:X5} Superclass: {superclassIndex}({superclassStr})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int classObjTree = BitConverter.ToInt32(data, offset);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"0x{offset:X5} NextItemCompilingChain: {classObjTree} {getEntryFullPath(classObjTree)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                UnrealFlags.EPropertyFlags ObjectFlagsMask = (UnrealFlags.EPropertyFlags)BitConverter.ToUInt64(data, offset);
                TreeViewItem objectFlagsNode = new TreeViewItem()
                {
                    Header = $"0x{offset:X5} ObjectFlags: 0x{(ulong)ObjectFlagsMask:X16}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                };

                topLevelTree.Items.Add(objectFlagsNode);

                //Create objectflags tree
                foreach (UnrealFlags.EPropertyFlags flag in Enum.GetValues(typeof(UnrealFlags.EPropertyFlags)))
                {
                    if ((ObjectFlagsMask & flag) != UnrealFlags.EPropertyFlags.None)
                    {
                        string reason = UnrealFlags.propertyflagsdesc[flag];
                        objectFlagsNode.Items.Add(new TreeViewItem()
                        {
                            Header = $"{(ulong)flag:X16} {flag} {(reason.Length > 0 ? reason : "")}",
                            Name = "_" + offset
                        });
                    }
                }
                offset += 8;

                int unk1 = BitConverter.ToInt32(data, offset);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"0x{offset:X5} Unknown1 {unk1}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                //has listed outerclass
                int none = BitConverter.ToInt32(data, offset);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"0x{offset:X5} None: {CurrentLoadedExport.FileRef.getNameEntry(none)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 8;

                int unk2 = BitConverter.ToInt32(data, offset);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"0x{offset:X5} Unknown2: {unk2}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4; //

                if (CurrentLoadedExport.ClassName == "ObjectProperty")
                {
                    //has listed outerclass
                    int outer = BitConverter.ToInt32(data, offset);
                    topLevelTree.Items.Add(new TreeViewItem()
                    {
                        Header = $"0x{offset:X5} OuterClass: {outer} {getEntryFullPath(outer)}",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;
                }
                else if (CurrentLoadedExport.ClassName == "ArrayProperty")
                {
                    //has listed outerclass
                    int outer = BitConverter.ToInt32(data, offset);
                    topLevelTree.Items.Add(new TreeViewItem()
                    {
                        Header = $"0x{offset:X5} Array can hold objects of type: {outer} {getEntryFullPath(outer)}",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;
                }
            }
            catch (Exception ex)
            {
                topLevelTree.Items.Add($"An error occured parsing the {CurrentLoadedExport.ClassName} binary: {ex.Message}");
            }

            /*treeView1.Nodes.Add(topLevelTree);
            treeView1.CollapseAll();
            treeView1.Nodes[0].Expand();
            TreeViewItem[] nodes;

            if (nodeNameToSelect != null)
            {
                nodes = treeView1.Nodes.Find(nodeNameToSelect, true);
                if (nodes.Length > 0)
                {
                    treeView1.SelectedNode = nodes[0];
                }
                else
                {
                    treeView1.SelectedNode = treeView1.Nodes[0];
                }
            }*/
        }

        private void Scan_WwiseStreamBank(TreeViewItem topLevelTree, byte[] data, int binaryStart)
        {
            /*
             * int32 0?
             * stream length in AFC +4 | (bank size)
             * stream length in AFC +4 | (repeat) (bank size)
             * stream offset in AFC +4 | (bank offset in file)
             */


            try
            {
                int pos = 0;
                if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
                {
                    pos = CurrentLoadedExport.propsEnd();
                }
                else if (CurrentLoadedExport.FileRef.Game == MEGame.ME2)
                {
                    pos = CurrentLoadedExport.propsEnd() + 0x20;
                }

                int Unk1 = BitConverter.ToInt32(data, pos);
                int DataSize = BitConverter.ToInt32(data, pos + 4);
                int DataSize2 = BitConverter.ToInt32(data, pos + 8);
                int DataOffset = BitConverter.ToInt32(data, pos + +0xC);

                int unk1 = BitConverter.ToInt32(data, pos);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"{pos:X4} Unknown: {unk1}",
                    Name = "_" + pos,
                });
                pos += 4;
                string dataset1type = CurrentLoadedExport.ClassName == "WwiseStream" ? "Stream length" : "Bank size";
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"{DataSize:X4} : {dataset1type} {DataSize} (0x{DataSize:X})",
                    Name = "_" + pos,
                    Tag = nodeType.StructLeafInt
                });
                pos += 4;
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"{ pos:X4} {dataset1type}: {DataSize2} (0x{ DataSize2:X})",
                    Name = "_" + pos,
                    Tag = nodeType.StructLeafInt
                });
                pos += 4;
                string dataset2type = CurrentLoadedExport.ClassName == "WwiseStream" ? "Stream offset" : "Bank offset";
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"{pos:X4} {dataset2type} in file: {DataOffset} (0x{DataOffset:X})",
                    Name = "_" + pos,
                    Tag = nodeType.StructLeafInt
                });

                if (CurrentLoadedExport.ClassName == "WwiseBank")
                {
                    //if (CurrentLoadedExport.DataOffset < DataOffset && (CurrentLoadedExport.DataOffset + CurrentLoadedExport.DataSize) < DataOffset)
                    //{
                    topLevelTree.Items.Add(new TreeViewItem()
                    {
                        Header = "Click here to jump to the calculated end offset of wwisebank in this export",
                        Name = "_" + (DataSize2 + CurrentLoadedExport.propsEnd() + 16),
                        Tag = nodeType.Unknown
                    });
                    //}
                }

                pos += 4;
                if (CurrentLoadedExport.ClassName == "WwiseStream")
                {
                    if (pos < data.Length && CurrentLoadedExport.GetProperty<NameProperty>("Filename") == null)
                    {
                        topLevelTree.Items.Add(new TreeViewItem()
                        {
                            Header = $"{pos:X4} Embedded sound data. Use Soundplorer to modify this data.",
                            Name = "_" + pos,
                            Tag = nodeType.Unknown
                        });
                        topLevelTree.Items.Add(new TreeViewItem()
                        {
                            Header = "The stream offset to this data will be automatically updated when this file is saved.",
                            Tag = nodeType.Unknown
                        });

                    }
                }
                else if (CurrentLoadedExport.ClassName == "WwiseBank")
                {
                    topLevelTree.Items.Add(new TreeViewItem()
                    {
                        Header = $"{pos:X4} Embedded soundbank. Use Soundplorer WPF to view data.",
                        Name = "_" + pos,
                        Tag = nodeType.Unknown
                    });
                    topLevelTree.Items.Add(new TreeViewItem()
                    {
                        Header = "The bank offset to this data will be automatically updated when this file is saved.",
                        Tag = nodeType.Unknown
                    });
                }
            }
            catch (Exception ex)
            {
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"Error reading binary data: {ex}"
                });
            }

            //topLevelTree.Expand();
            //treeView1.Nodes[0].Expand();
        }

        private void Scan_WwiseEvent(TreeViewItem topLevelTree, byte[] data, int binarystart)
        {
            try
            {
                int binarypos = binarystart;
                List<TreeViewItem> subnodes = new List<TreeViewItem>();
                int count = BitConverter.ToInt32(data, binarypos);
                subnodes.Add(new TreeViewItem() { Header = $"0x{binarypos:X4} Count: {count.ToString()}" });
                binarypos += 4; //+ int
                if (count > 0)
                {
                    string nodeText = $"0x{binarypos:X4} ";
                    int val = BitConverter.ToInt32(data, binarypos);
                    string name = val.ToString();
                    if (val > 0 && val <= CurrentLoadedExport.FileRef.Exports.Count)
                    {
                        IExportEntry exp = CurrentLoadedExport.FileRef.Exports[val - 1];
                        nodeText += $"{name} {exp.PackageFullName}.{exp.ObjectName} ({exp.ClassName})";
                    }
                    else if (val < 0 && val != int.MinValue && Math.Abs(val) <= CurrentLoadedExport.FileRef.Imports.Count)
                    {
                        int csImportVal = Math.Abs(val) - 1;
                        ImportEntry imp = CurrentLoadedExport.FileRef.Imports[csImportVal];
                        nodeText += $"{name} {imp.PackageFullName}.{imp.ObjectName} ({imp.ClassName})";
                    }

                    subnodes.Add(new TreeViewItem()
                    {
                        Header = nodeText,
                        Tag = nodeType.StructLeafObject,
                        Name = "_" + binarypos
                    });
                    /*

                                        int objectindex = BitConverter.ToInt32(data, binarypos);
                                        IEntry obj = pcc.getEntry(objectindex);
                                        string nodeValue = obj.GetFullPath;
                                        node.Tag = nodeType.StructLeafObject;
                                        */
                }
                topLevelTree.ItemsSource = subnodes;
            }
            catch (Exception ex)
            {
                topLevelTree.Items.Add($"An error occured parsing the wwiseevent: {ex.Message}");
            }
        }

        private void StartBioDynamicAnimSetScan(TreeViewItem topLevelTree, byte[] data, int binarystart)
        {
            try
            {
                int binarypos = binarystart;
                List<TreeViewItem> subnodes = new List<TreeViewItem>();
                int count = BitConverter.ToInt32(data, binarypos);
                subnodes.Add(new TreeViewItem()
                {
                    Header = $"0x{binarypos:X4} Count: {count.ToString()}"
                });
                binarypos += 4; //+ int
                for (int i = 0; i < count; i++)
                {
                    int nameIndex = BitConverter.ToInt32(data, binarypos);
                    int nameIndexNum = BitConverter.ToInt32(data, binarypos + 4);
                    int shouldBe1 = BitConverter.ToInt32(data, binarypos + 8);
                    string nodeValue = $"{CurrentLoadedExport.FileRef.Names[nameIndex]}_{nameIndexNum}";
                    if (shouldBe1 != 1)
                    {
                        //ERROR
                        nodeValue += " - Not followed by 1 (integer)!";
                    }

                    subnodes.Add(new TreeViewItem()
                    {
                        Header = $"0x{binarypos:X4} Name: {nodeValue}",
                        Tag = NodeType.StructLeafName,
                        Name = $"_{binarypos.ToString()}"
                    });
                    binarypos += 12;
                }
                subnodes.ForEach(o => topLevelTree.Items.Add(o));
            }
            catch (Exception ex)
            {
                topLevelTree.Items.Add($"An error occured parsing the biodynamicanimset: {ex.Message}");
            }
        }

        //TODO: unfinished. currently does not display the properties for the list of BioStageCamera objects at the end
        private void StartBioStageScan(TreeViewItem topLevelTree, byte[] data, int binarystart)
        {
            /*
             * Length (int)
                Name: m_aCameraList
                int unknown 0
                Count + int unknown
                [Camera name
                    unreal property data]*/

            if ((CurrentLoadedExport.Header[0x1f] & 0x2) != 0)
            {
                List<TreeViewItem> subnodes = new List<TreeViewItem>();

                int pos = binarystart;
                int length = BitConverter.ToInt32(data, binarystart);
                subnodes.Add(new TreeViewItem()
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
                    subnodes.Add(new TreeViewItem
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
                    subnodes.Add(new TreeViewItem
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
                            TreeViewItem parentnode = new TreeViewItem
                            {
                                Header = $"{pos:X4} Camera {i + 1}: {CurrentLoadedExport.FileRef.getNameEntry(nameindex)}_{nameindexunreal}",
                                Tag = NodeType.StructLeafName,
                                Name = $"_{pos.ToString()}"
                            };
                            subnodes.Add(parentnode);
                            pos += 8;
                            stream.Seek(pos, SeekOrigin.Begin);
                            var props = PropertyCollection.ReadProps(CurrentLoadedExport.FileRef, stream, "BioStageCamera");
                            //finish writing function here
                            pos = props.endOffset;

                        }
                    }
                    catch (Exception ex)
                    {
                        subnodes.Add(new TreeViewItem { Header = $"Error reading binary data: {ex}" });
                    }
                }
                topLevelTree.ItemsSource = subnodes;
            }
        }

        private void StartClassScan(TreeViewItem topLevelTree, byte[] data, int binarystart)
        {
            //const int nonTableEntryCount = 2; //how many items we parse that are not part of the functions table. e.g. the count, the defaults pointer

            try
            {
                var subnodes = new List<TreeViewItem>();
                int offset = 0;

                int unrealExportIndex = BitConverter.ToInt32(data, offset);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{offset:X5} Unreal Unique Index: {unrealExportIndex}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;


                int superclassIndex = BitConverter.ToInt32(data, offset);
                string superclassStr = getEntryFullPath(superclassIndex);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{offset:X5} Superclass Index: {superclassIndex}({superclassStr})",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int unknown1 = BitConverter.ToInt32(data, offset);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{offset:X5} Unknown 1: {unknown1}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{offset:X5} ProbeMask/Class Object Tree Final Pointer Index: {classObjTree}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;


                //I am not sure what these mean. However if Pt1&2 are 33/25, the following bytes that follow are extended.
                int headerUnknown1 = BitConverter.ToInt32(data, offset);
                Int64 ignoreMask = BitConverter.ToInt64(data, offset);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{offset:X5} IgnoreMask: 0x{ignoreMask:X16}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 8;

                Int16 labelOffset = BitConverter.ToInt16(data, offset);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{offset:X5} LabelOffset: 0x{labelOffset:X4}",
                    Name = "_" + offset

                });
                offset += 2;

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
                    else
                    {
                        skipAmount++;
                    }
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
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{offset:X5} State/Script Block: 0x{offset:X4} - 0x{offsetEnd:X4}",
                    Name = "_" + offset

                });
                offset += skipAmount + 10; //heuristic to find end of script
                                           //for (int i = 0; i < 5; i++)
                                           //{
                uint stateMask = BitConverter.ToUInt32(data, offset);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{offset:X5} Statemask: {stateMask} [{getStateFlagsStr(stateMask)}]",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;
                //}
                //offset += 2; //oher unknown
                int localFunctionsTableCount = BitConverter.ToInt32(data, offset);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{offset:X5} Local Functions Count: {localFunctionsTableCount}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;
                for (int i = 0; i < localFunctionsTableCount; i++)
                {
                    int nameTableIndex = BitConverter.ToInt32(data, offset);
                    int nameIndex = BitConverter.ToInt32(data, offset + 4);
                    offset += 8;
                    int functionObjectIndex = BitConverter.ToInt32(data, offset);
                    offset += 4;
                    subnodes.Last().Items.Add(new TreeViewItem
                    {
                        Header = $"0x{offset - 12:X5}  {CurrentLoadedExport.FileRef.getNameEntry(nameTableIndex)}() = {functionObjectIndex}({CurrentLoadedExport.FileRef.Exports[functionObjectIndex - 1].GetFullPath})",
                        Name = "_" + (offset - 12),

                        Tag = NodeType.StructLeafName //might need to add a subnode for the 3rd int
                    });
                }

                int classMask = BitConverter.ToInt32(data, offset);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{offset:X5} Class Mask: 0x{classMask:X8}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                if (CurrentLoadedExport.FileRef.Game != MEGame.ME3)
                {
                    offset += 1; //seems to be a blank byte here
                }

                int coreReference = BitConverter.ToInt32(data, offset);
                string coreRefFullPath = getEntryFullPath(coreReference);

                subnodes.Add(new TreeViewItem
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
                    int postComponentNoneIndex = BitConverter.ToInt32(data, offset + 4);
                    string postCompName = CurrentLoadedExport.FileRef.getNameEntry(postComponentsNoneNameIndex); //This appears to be unused in ME#, it is always None it seems.
                                                                                                                 /*if (postCompName != "None")
                                                                                                                 {
                                                                                                                     Debugger.Break();
                                                                                                                 }*/
                    subnodes.Add(new TreeViewItem
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
                    subnodes.Add(new TreeViewItem
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
                    node = new TreeViewItem($"0x{offset:X5} Unknown 4: {unknown4);
                    node.Name = offset.ToString();
                    node.Tag = nodeType.StructLeafInt;
                    subnodes.Add(node);
                    offset += 4;*/

                    int me12unknownend1 = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new TreeViewItem
                    {
                        Header = $"0x{offset:X5} ME1/ME2 Unknown1: {me12unknownend1}",
                        Name = "_" + offset,

                        Tag = NodeType.StructLeafName
                    });
                    offset += 4;

                    int me12unknownend2 = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new TreeViewItem
                    {
                        Header = $"0x{offset:X5} ME1/ME2 Unknown2: {me12unknownend2}",
                        Name = "_" + offset,

                        Tag = NodeType.StructLeafName
                    });
                    offset += 4;
                }

                int defaultsClassLink = BitConverter.ToInt32(data, offset);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{offset:X5} Class Defaults: {defaultsClassLink} ({CurrentLoadedExport.FileRef.Exports[defaultsClassLink - 1].GetFullPath})",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
                {
                    int functionsTableCount = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new TreeViewItem
                    {
                        Header = $"0x{offset:X5} Full Functions Table Count: {functionsTableCount}",
                        Name = "_" + offset,

                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    for (int i = 0; i < functionsTableCount; i++)
                    {
                        int functionsTableIndex = BitConverter.ToInt32(data, offset);
                        string impexpName = getEntryFullPath(functionsTableIndex);
                        subnodes.Last().Items.Add(new TreeViewItem
                        {
                            Header = $"0x{offset:X5} {impexpName}",
                            Tag = NodeType.StructLeafObject,
                            Name = "_" + offset

                        });
                        offset += 4;
                    }
                }
                topLevelTree.ItemsSource = subnodes;
            }
            catch (Exception ex)
            {
                topLevelTree.Items.Add($"An error occured parsing the class: {ex.Message}");
            }
        }

        private int ClassParser_ReadComponentsTable(List<TreeViewItem> subnodes, byte[] data, int offset)
        {
            if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
            {
                int componentTableNameIndex = BitConverter.ToInt32(data, offset);
                int componentTableIndex = BitConverter.ToInt32(data, offset + 4);
                offset += 8;

                subnodes.Add(new TreeViewItem
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
                    int nameIndex = BitConverter.ToInt32(data, offset + 4);
                    offset += 8;
                    int componentObjectIndex = BitConverter.ToInt32(data, offset);
                    offset += 4;
                    string objectName = getEntryFullPath(componentObjectIndex);
                    subnodes.Last().Items.Add(new TreeViewItem
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
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{offset:X5} Components Table Count: {componentTableCount}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                for (int i = 0; i < componentTableCount; i++)
                {
                    int nameTableIndex = BitConverter.ToInt32(data, offset);
                    int nameIndex = BitConverter.ToInt32(data, offset + 4);
                    offset += 8;
                    int componentObjectIndex = BitConverter.ToInt32(data, offset);

                    string objName = "Null";
                    if (componentObjectIndex != 0)
                    {
                        objName = getEntryFullPath(componentObjectIndex);
                    }
                    subnodes.Last().Items.Add(new TreeViewItem
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

        private int ClassParser_ReadImplementsTable(List<TreeViewItem> subnodes, byte[] data, int offset)
        {
            if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
            {
                int interfaceCount = BitConverter.ToInt32(data, offset);

                subnodes.Add(new TreeViewItem
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

                    string objectName = getEntryFullPath(interfaceIndex);
                    TreeViewItem subnode = new TreeViewItem
                    {
                        Header = $"0x{offset - 12:X5}  {interfaceIndex} {objectName}",
                        Name = "_" + (offset - 4),
                        Tag = NodeType.StructLeafName
                    };
                    subnodes.Last().Items.Add(subnode);

                    //propertypointer
                    interfaceIndex = BitConverter.ToInt32(data, offset);
                    offset += 4;

                    objectName = getEntryFullPath(interfaceIndex);
                    subnode.Items.Add(new TreeViewItem
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
                subnodes.Add(new TreeViewItem
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

                    TreeViewItem subnode = new TreeViewItem
                    {
                        Header = $"0x{offset - 8:X5}  {CurrentLoadedExport.FileRef.getNameEntry(interfaceNameIndex)}",
                        Name = "_" + (offset - 8),

                        Tag = NodeType.StructLeafName
                    };
                    subnodes.Last().Items.Add(subnode);

                    //propertypointer
                    /* interfaceIndex = BitConverter.ToInt32(data, offset);
                     offset += 4;

                     objectName = getEntryFullPath(interfaceIndex);
                     TreeNode subsubnode = new TreeNode($"0x{offset - 12:X5}  Interface Property Link: {interfaceIndex} {objectName}");
                     subsubnode.Name = (offset - 4).ToString();
                     subsubnode.Tag = nodeType.StructLeafObject;
                     subnode.Nodes.Add(subsubnode);
                     */
                }
            }
            return offset;
        }

        private void StartEnumScan(TreeViewItem topLevelTree, byte[] data, int binarystart)
        {
            try
            {
                var subnodes = new List<TreeViewItem>();
                int offset = 0;
                int unrealExportIndex = BitConverter.ToInt32(data, offset);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{offset:X5} Unreal Unique Index: {unrealExportIndex}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int noneUnrealProperty = BitConverter.ToInt32(data, offset);
                int noneUnrealPropertyIndex = BitConverter.ToInt32(data, offset + 4);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{offset:X5} Unreal property None Name: {CurrentLoadedExport.FileRef.getNameEntry(noneUnrealProperty)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafName
                });
                offset += 8;

                int superclassIndex = BitConverter.ToInt32(data, offset);
                string superclassStr = getEntryFullPath(superclassIndex);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{offset:X5} Superclass: {superclassIndex}({superclassStr})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{offset:X5} NextItemCompilingChain: {classObjTree} {getEntryFullPath(classObjTree)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                if (CurrentLoadedExport.ClassName == "Enum")
                {

                    int enumSize = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new TreeViewItem
                    {
                        Header = $"0x{offset:X5} Enum Size: {enumSize}",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    for (int i = 0; i < enumSize; i++)
                    {
                        int enumName = BitConverter.ToInt32(data, offset);
                        int enumNameIndex = BitConverter.ToInt32(data, offset + 4);
                        subnodes.Add(new TreeViewItem
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
                    subnodes.Add(new TreeViewItem
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
                        string str = stream.ReadString((literalStringLength * -2), true, Encoding.Unicode);
                        subnodes.Add(new TreeViewItem
                        {
                            Header = $"0x{offset:X5} Const Literal Value: {str}",
                            Name = "_" + offset,
                            Tag = NodeType.StrProperty
                        });
                    }
                }
                topLevelTree.ItemsSource = subnodes;
            }
            catch (Exception ex)
            {
                topLevelTree.Items.Add($"An error occured parsing the {CurrentLoadedExport.ClassName} binary: {ex.Message}");
            }
        }

        private void StartGuidCacheScan(TreeViewItem topLevelTree, byte[] data, int binarystart)
        {
            /*
             *  
             *  count +4
             *      nameentry +8
             *      guid +16
             *      
             */

            try
            {
                var subnodes = new List<TreeViewItem>();
                int pos = binarystart;
                int count = BitConverter.ToInt32(data, pos);
                subnodes.Add(new TreeViewItem
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
                    subnodes.Add(new TreeViewItem
                    {
                        Header = $"{pos:X4} {CurrentLoadedExport.FileRef.getNameEntry(nameRef)}_{nameIdx}: {{{guid}}}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafName
                    });
                    pos += 24;
                }
                topLevelTree.ItemsSource = subnodes;
            }
            catch (Exception ex)
            {
                topLevelTree.Items.Add($"Error reading binary data: {ex}");
            }
        }

        private void StartLevelScan(TreeViewItem topLevelTree, byte[] data, int binarystart)
        {
            try
            {
                var subnodes = new List<TreeViewItem>();
                //find start of class binary (end of props)
                int start = 0x4;
                while (start < data.Length)
                {
                    uint nameindex = BitConverter.ToUInt32(data, start);
                    if (nameindex < CurrentLoadedExport.FileRef.Names.Count && CurrentLoadedExport.FileRef.Names[(int)nameindex] == "None")
                    {
                        //found it
                        start += 8;
                        break;
                    }
                    else
                    {
                        start += 4;
                    }
                }

                //Console.WriteLine("Found start of binary at {start.ToString("X8"));

                uint exportid = BitConverter.ToUInt32(data, start);
                start += 4;
                uint numberofitems = BitConverter.ToUInt32(data, start);
                int countoffset = start;
                TreeViewItem countnode = new TreeViewItem
                {
                    Tag = NodeType.Unknown,
                    Header = $"{start:X4} Level Items List Length: {numberofitems}",
                    Name = "_" + start

                };
                subnodes.Add(countnode);


                start += 4;
                uint bioworldinfoexportid = BitConverter.ToUInt32(data, start);
                TreeViewItem bionode = new TreeViewItem
                {
                    Tag = NodeType.StructLeafObject,
                    Header = $"{start:X4} BioWorldInfo Export: {bioworldinfoexportid}",
                    Name = "_" + start

                };
                if (bioworldinfoexportid < CurrentLoadedExport.FileRef.ExportCount && bioworldinfoexportid > 0)
                {
                    int me3expindex = (int)bioworldinfoexportid;
                    IEntry exp = CurrentLoadedExport.FileRef.getEntry(me3expindex);
                    bionode.Header += $" ({exp.PackageFullName}.{exp.ObjectName})";
                }
                subnodes.Add(bionode);

                IExportEntry bioworldinfo = CurrentLoadedExport.FileRef.Exports[(int)bioworldinfoexportid - 1];
                if (bioworldinfo.ObjectName != "BioWorldInfo")
                {
                    subnodes.Add(new TreeViewItem
                    {
                        Tag = NodeType.Unknown,
                        Header = $"{start:X4} Export pointer to bioworldinfo resolves to wrong export. Resolved to {bioworldinfo.ObjectName} as export {bioworldinfoexportid}",
                        Name = "_" + start

                    });
                    topLevelTree.ItemsSource = subnodes;
                    return;
                }

                start += 4;
                uint shouldbezero = BitConverter.ToUInt32(data, start);
                if (shouldbezero != 0)
                {
                    subnodes.Add(new TreeViewItem
                    {
                        Tag = NodeType.Unknown,
                        Header = $"{start:X4} Export may have extra parameters not accounted for yet (did not find 0 at 0x{start:X5} )",
                        Name = "_" + start

                    });
                    topLevelTree.ItemsSource = subnodes;
                    return;
                }
                start += 4;
                int itemcount = 2; //Skip bioworldinfo and Class

                while (itemcount < numberofitems)
                {
                    //get header.
                    uint itemexportid = BitConverter.ToUInt32(data, start);
                    if (itemexportid - 1 < CurrentLoadedExport.FileRef.Exports.Count)
                    {
                        IExportEntry locexp = CurrentLoadedExport.FileRef.Exports[(int)itemexportid - 1];
                        //Console.WriteLine($"0x{start:X5} \t0x{itemexportid:X5} \t{locexp.PackageFullName}.{locexp.ObjectName}_{locexp.indexValue} [{itemexportid - 1}]");
                        subnodes.Add(new TreeViewItem
                        {
                            Tag = NodeType.ArrayLeafObject,
                            Header = $"{start:X4}|{itemcount}: {locexp.PackageFullName}.{locexp.ObjectName}_{locexp.indexValue} [{itemexportid - 1}]",
                            Name = "_" + start

                        });
                        start += 4;
                        itemcount++;
                    }
                    else
                    {
                        Console.WriteLine($"0x{start:X5} \t0x{itemexportid:X5} \tInvalid item. Ensure the list is the correct length. (Export {itemexportid})");
                        subnodes.Add(new TreeViewItem
                        {
                            Tag = NodeType.ArrayLeafObject,
                            Header = $"{start:X4} Invalid item.Ensure the list is the correct length. (Export {itemexportid})",
                            Name = "_" + start

                        });
                        start += 4;
                        itemcount++;
                    }
                }
                topLevelTree.ItemsSource = subnodes;
            }
            catch (Exception e)
            {
                topLevelTree.Items.Add($"Error parsing level: {e.Message}");
            }
        }

        private void StartMaterialScan(TreeViewItem topLevelTree, byte[] data, int binarystart)
        {
            const int nonTableEntryCount = 2; //how many items we parse that are not part of the functions table. e.g. the count, the defaults pointer
            
            if (binarystart >= data.Length)
            {
                topLevelTree.Items.Add("No Binary Data");
                return;
            }
            try
            {
                var subnodes = new List<TreeViewItem>();
                int binarypos = binarystart;

                binarypos += 0x20; //Skip ??? and GUID

                int count = BitConverter.ToInt32(data, binarypos);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"0x{binarypos:X4} Count: {count}",
                    Name = "_" + binarypos

                });
                binarypos += 4;

                while (binarypos <= data.Length - 4 && count > 0)
                {
                    int val = BitConverter.ToInt32(data, binarypos);
                    string name = val.ToString();

                    if (val > 0 && val <= CurrentLoadedExport.FileRef.Exports.Count)
                    {
                        IExportEntry exp = CurrentLoadedExport.FileRef.Exports[val - 1];
                        name += $" {exp.PackageFullName}.{exp.ObjectName} ({exp.ClassName})";
                    }
                    else if (val < 0 && Math.Abs(val) <= CurrentLoadedExport.FileRef.Imports.Count)
                    {
                        int csImportVal = Math.Abs(val) - 1;
                        ImportEntry imp = CurrentLoadedExport.FileRef.Imports[csImportVal];
                        name += $" {imp.PackageFullName}.{imp.ObjectName} ({imp.ClassName})";

                    }
                    subnodes.Add(new TreeViewItem
                    {
                        Header = $"0x{binarypos:X4} {name}",
                        Tag = NodeType.StructLeafObject,
                        Name = "_" + binarypos

                    });
                    binarypos += 4;
                    count--;
                }

                topLevelTree.ItemsSource = subnodes;
                subnodes.Add(new TreeViewItem{ Header = "There's a bunch more binary in this object, guids and name refs and object refs." });
                subnodes.Add(new TreeViewItem{ Header = "Unfortunately this tool is not smart enough to understand them, but you might be able to." });
                subnodes.Add(new TreeViewItem{ Header = "This is your chance to prove that humans are still better than machines." });
            }
            catch (Exception ex)
            {
                topLevelTree.Items.Add($"An error occured parsing the material: {ex.Message}");
            }
        }

        private void StartPrefabInstanceScan(TreeViewItem topLevelTree, byte[] data, int binarystart)
        {
            /*
             *  count: 4 bytes 
             *      Prefab ref : 4 bytes
             *      Level Object : 4 bytes
             *  0: 4 bytes
             *  
             */

            if ((CurrentLoadedExport.Header[0x1f] & 0x2) == 0)
            {
                return;
            }

            try
            {
                var subnodes = new List<TreeViewItem>();
                int pos = binarystart;
                int count = BitConverter.ToInt32(data, pos);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"{pos:X4} Count: {count}",
                    Name = "_" + pos

                });
                pos += 4;
                while (pos + 8 <= data.Length && count > 0)
                {
                    var exportRef = BitConverter.ToInt32(data, pos);
                    subnodes.Add(new TreeViewItem
                    {
                        Header = $"{pos:X4}: {exportRef} Prefab: {CurrentLoadedExport.FileRef.getEntry(exportRef).GetFullPath}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafObject
                    });
                    pos += 4;
                    exportRef = BitConverter.ToInt32(data, pos);
                    if (exportRef == 0)
                    {
                        subnodes.Last().Items.Add(new TreeViewItem
                        {
                            Header = $"{pos:X4}: {exportRef} Level Object: Null",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafObject
                        });
                    }
                    else
                    {
                        subnodes.Last().Items.Add(new TreeViewItem
                        {
                            Header = $"{pos:X4}: {exportRef} Level Object: {CurrentLoadedExport.FileRef.getEntry(exportRef).GetFullPath}",
                            Name = "_" + pos,

                            Tag = NodeType.StructLeafObject
                        });
                    }

                    pos += 4;
                    count--;
                }

                topLevelTree.ItemsSource = subnodes;
            }
            catch (Exception ex)
            {
                topLevelTree.Items.Add($"Error reading binary data: {ex}");
            }
        }

        private void StartSkeletalMeshScan(TreeViewItem topLevelTree, byte[] data, int binarystart)
        {
            /*
             *  
             *  Bounding +28
             *  count +4
             *      materials
             *  
             */
            try
            {
                var subnodes = new List<TreeViewItem>();
                int pos = binarystart;
                pos += 28; //bounding
                int count = BitConverter.ToInt32(data, pos);
                subnodes.Add(new TreeViewItem
                {
                    Header = $"{pos:X4} Material Count: {count}",
                    Name = "_" + pos,

                });
                pos += 4;
                for (int i = 0; i < count; i++)
                {
                    int material = BitConverter.ToInt32(data, pos);
                    subnodes.Add(new TreeViewItem
                    {
                        Header = $"{pos:X4} Material: ({material}) {CurrentLoadedExport.FileRef.getEntry(material)?.GetFullPath ?? ""}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafObject
                    });
                    pos += 4;
                }

                topLevelTree.ItemsSource = subnodes;
            }
            catch (Exception ex)
            {
                topLevelTree.Items.Add($"Error reading binary data: {ex}");
            }
        }

        private void StartStaticMeshCollectionActorScan(TreeViewItem topLevelTree, byte[] data, int binarystart)
        {
            try
            {
                var subnodes = new List<TreeViewItem>();
                //get a list of staticmesh stuff from the props.
                var smacitems = new List<IExportEntry>();
                var props = CurrentLoadedExport.GetProperty<ArrayProperty<ObjectProperty>>("StaticMeshComponents");

                foreach (var prop in props)
                {
                    if (prop.Value > 0)
                    {
                        smacitems.Add(CurrentLoadedExport.FileRef.getEntry(prop.Value) as IExportEntry);
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
                    topLevelTree.Items.Add(new TreeViewItem
                    {
                        Tag = NodeType.Unknown,
                        Header = $"{start:X4} Binary data is not divisible by 64 ({data.Length - start})! SMCA binary data should be a length divisible by 64.",
                        Name = "_" + start

                    });
                    return;
                }

                int smcaindex = 0;
                while (start < data.Length && smcaindex < smacitems.Count)
                {
                    TreeViewItem smcanode = new TreeViewItem
                    {
                        Tag = NodeType.Unknown
                    };
                    IExportEntry assossiateddata = smacitems[smcaindex];
                    string staticmesh = "";
                    string objtext = "Null - unused data";
                    if (assossiateddata != null)
                    {
                        objtext = $"[Export {assossiateddata.Index}] {assossiateddata.ObjectName}_{assossiateddata.indexValue}";

                        //find associated static mesh value for display.
                        byte[] smc_data = assossiateddata.Data;
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
                        TreeViewItem node = new TreeViewItem
                        {
                            Tag = NodeType.StructLeafFloat,
                            Header = start.ToString("X4")
                        };

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

                        //Lookup staticmeshcomponent so we can see what this actually is without flipping
                        // export

                        node.Name = "_" + start;
                        smcanode.Items.Add(node);
                        start += 4;
                    }

                    smcaindex++;
                }

                topLevelTree.ItemsSource = subnodes;

            }
            catch (Exception ex)
            {
                topLevelTree.Items.Add($"An error occured parsing the staticmesh: {ex.Message}");
            }
        }

        private void StartStaticMeshScan(TreeViewItem topLevelTree, byte[] data, int binarystart)
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
            try
            {
                var subnodes = new List<TreeViewItem>();
                int pos = binarystart;
                pos += 28;
                int rbRef = BitConverter.ToInt32(data, pos);
                subnodes.Add(new TreeViewItem
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
                        subnodes.Add(new TreeViewItem
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

                topLevelTree.ItemsSource = subnodes;
            }
            catch (Exception ex)
            {
                topLevelTree.Items.Add($"Error reading binary data: {ex}");
            }
        }
        #endregion

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
            BinaryInterpreter_Hexbox.ByteProvider = new DynamicByteProvider(new byte[] { });
            BinaryInterpreter_TreeView.Items.Clear();
        }

        private void BinaryInterpreter_SaveHexChanges_Click(object sender, RoutedEventArgs e)
        {

        }

        private void BinaryInterpreter_TreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (BinaryInterpreter_TreeView.SelectedItem is TreeViewItem tvi 
                && tvi.Name is string tag
                && tag.StartsWith("_"))
            {
                tag = tag.Substring(1); //remove _
                if (int.TryParse(tag, out int result))
                {
                    BinaryInterpreter_Hexbox.SelectionStart = result;
                    BinaryInterpreter_Hexbox.SelectionLength = 1;
                }
            }
        }

        private void BinaryInterpreter_TreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void BinaryInterpreter_ToggleHexboxWidth_Click(object sender, RoutedEventArgs e)
        {

        }

        private void BinaryInterpreter_Loaded(object sender, RoutedEventArgs e)
        {
            BinaryInterpreter_Hexbox = (HexBox)BinaryInterpreter_Hexbox_Host.Child;
        }

        private void hb1_SelectionChanged(object sender, EventArgs e)
        {

            int start = (int)BinaryInterpreter_Hexbox.SelectionStart;
            int len = (int)BinaryInterpreter_Hexbox.SelectionLength;
            int size = (int)BinaryInterpreter_Hexbox.ByteProvider.Length;
            byte[] currentData = (BinaryInterpreter_Hexbox.ByteProvider as DynamicByteProvider)?.Bytes.ToArray();
            try
            {
                if (currentData != null && start != -1 && start < size)
                {
                    string s = $"Byte: {currentData[start]}"; //if selection is same as size this will crash.
                    if (start <= currentData.Length - 4)
                    {
                        int val = BitConverter.ToInt32(currentData, start);
                        s += $", Int: {val}";
                        if (CurrentLoadedExport.FileRef.isName(val))
                        {
                            s += $", Name: {CurrentLoadedExport.FileRef.getNameEntry(val)}";
                        }
                        if (CurrentLoadedExport.FileRef.getEntry(val) is IExportEntry exp)
                        {
                            s += $", Export: {exp.ObjectName}";
                        }
                        else if (CurrentLoadedExport.FileRef.getEntry(val) is ImportEntry imp)
                        {
                            s += $", Import: {imp.ObjectName}";
                        }
                    }
                    s += $" | Start=0x{start:X8} ";
                    if (len > 0)
                    {
                        s += $"Length=0x{len:X8} ";
                        s += $"End=0x{(start + len - 1):X8}";
                    }
                    s += $" | File offset: {(CurrentLoadedExport.DataOffset + start):X8}";
                    StatusBar_LeftMostText.Text = s;
                }
                else
                {
                    StatusBar_LeftMostText.Text = "Nothing Selected";
                }
            }
            catch (Exception)
            {
            }
        }

        private string getEntryFullPath(int index)
        {
            if (index == 0)
            {
                return "Null";
            }
            string retStr = "Entry not found";
            IEntry coreRefEntry = CurrentLoadedExport.FileRef.getEntry(index);
            if (coreRefEntry != null)
            {
                retStr = coreRefEntry is ImportEntry ? "[I] " : "[E] ";
                retStr += coreRefEntry.GetFullPath;
            }
            return retStr;
        }
    }
}
