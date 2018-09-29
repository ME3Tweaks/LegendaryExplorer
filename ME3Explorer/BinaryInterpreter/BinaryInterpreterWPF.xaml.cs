using System;
using System.Collections.Generic;
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

        public override bool CanParse(IExportEntry exportEntry)
        {
            string[] ParsableBinaryClasses = { "Level", "StaticMeshCollectionActor", "StaticLightCollectionActor", "SeekFreeShaderCache", "Class", "BioStage", "ObjectProperty", "Const",
            "Enum", "ArrayProperty","FloatProperty", "IntProperty", "BoolProperty","Enum","ObjectRedirector", "WwiseEvent", "Material", "StaticMesh", "MaterialInstanceConstant",
            "BioDynamicAnimSet", "StaticMeshComponent", "SkeletalMeshComponent", "SkeletalMesh", "PrefabInstance",
            "WwiseStream", "TextureMovie", "GuidCache", "World", "Texture2D"};

            if (ParsableBinaryClasses.Contains(exportEntry.ClassName))
            {
                return true;
            }
            return false;
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
                case "WwiseStream":
                    Scan_WwiseStream(topLevelTree, data, binarystart);
                    break;
                case "WwiseEvent":
                    Scan_WwiseEvent(topLevelTree, data, binarystart);
                    break;
            }
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
                    Name = "_"+offset.ToString(),
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int noneUnrealProperty = BitConverter.ToInt32(data, offset);
                int noneUnrealPropertyIndex = BitConverter.ToInt32(data, offset + 4);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"0x{offset:X5} Unreal property None Name: {CurrentLoadedExport.FileRef.getNameEntry(noneUnrealProperty)}",
                    Name = "_"+offset.ToString(),
                    Tag = NodeType.StructLeafName
                });
                offset += 8;

                int superclassIndex = BitConverter.ToInt32(data, offset);
                string superclassStr = getEntryFullPath(superclassIndex);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"0x{offset:X5} Superclass: {superclassIndex}({superclassStr})",
                    Name = "_"+offset.ToString(),
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int classObjTree = BitConverter.ToInt32(data, offset);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"0x{offset:X5} NextItemCompilingChain: {classObjTree} {getEntryFullPath(classObjTree)}",
                    Name = "_"+offset.ToString(),
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                UnrealFlags.EPropertyFlags ObjectFlagsMask = (UnrealFlags.EPropertyFlags)BitConverter.ToUInt64(data, offset);
                TreeViewItem objectFlagsNode = new TreeViewItem()
                {
                    Header = $"0x{offset:X5} ObjectFlags: 0x{(ulong)ObjectFlagsMask:X16}",
                    Name = "_"+offset.ToString(),
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
                            Name = "_"+offset.ToString()
                        });
                    }
                }
                offset += 8;

                int unk1 = BitConverter.ToInt32(data, offset);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"0x{offset:X5} Unknown1 {unk1}",
                    Name = "_"+offset.ToString(),
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                //has listed outerclass
                int none = BitConverter.ToInt32(data, offset);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"0x{offset:X5} None: {CurrentLoadedExport.FileRef.getNameEntry(none)}",
                    Name = "_"+offset.ToString(),
                    Tag = NodeType.StructLeafInt
                });
                offset += 8;

                int unk2 = BitConverter.ToInt32(data, offset);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"0x{offset:X5} Unknown2: {unk2}",
                    Name = "_"+offset.ToString(),
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
                        Name = "_"+offset.ToString(),
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
                        Name = "_"+offset.ToString(),
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

        private void Scan_WwiseStream(TreeViewItem topLevelTree, byte[] data, int binaryStart)
        {
            /*
             * stream length in AFC +4
             * stream length in AFC +4 (repeat)
             * stream offset in AFC +4
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
                    Name = "_" + pos.ToString(),
                });
                pos += 4;
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"{DataSize:X4} Stream length: {DataSize} (0x{DataSize:X})",
                    Name = "_" + pos.ToString(),
                    Tag = nodeType.StructLeafInt
                });
                pos += 4;
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"{ pos:X4} Stream length: {DataSize2} (0x{ DataSize2:X})",
                    Name = "_" + pos.ToString(),
                    Tag = nodeType.StructLeafInt
                });
                pos += 4;
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"{pos:X4} Stream offset in file: {DataOffset} (0x{DataOffset:X})",
                    Name = "_" + pos.ToString(),
                    Tag = nodeType.StructLeafInt
                });
                pos += 4;
                if (pos < data.Length && CurrentLoadedExport.GetProperty<NameProperty>("Filename") == null)
                {
                    topLevelTree.Items.Add(new TreeViewItem()
                    {
                        Header = $"{pos:X4} Embedded sound data. Use Soundplorer to modify this data.",
                        Name = "_" + pos.ToString(),
                        Tag = nodeType.Unknown
                    });
                    topLevelTree.Items.Add(new TreeViewItem()
                    {
                        Header = $"The stream offset to this data will be automatically updated when this file is saved.",
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
                        Name = "_" + binarypos.ToString()
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
        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
            BinaryInterpreter_Hexbox.ByteProvider = new DynamicByteProvider(new byte[] { });
            BinaryInterpreter_TreeView.Items.Clear();
        }

        private void BinaryInterpreter_SaveHexChanged_Click(object sender, RoutedEventArgs e)
        {

        }

        private void BinaryInterpreter_TreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {

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
            byte[] currentData = (BinaryInterpreter_Hexbox.ByteProvider as DynamicByteProvider).Bytes.ToArray();
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
                    s += $" | Start=0x{start.ToString("X8")} ";
                    if (len > 0)
                    {
                        s += $"Length=0x{len.ToString("X8")} ";
                        s += $"End=0x{(start + len - 1).ToString("X8")}";
                    }
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
                if (coreRefEntry is ImportEntry)
                {
                    retStr = "[I] ";
                }
                else
                {
                    retStr = "[E] ";
                }
                retStr += coreRefEntry.GetFullPath;
            }
            return retStr;
        }
    }
}
