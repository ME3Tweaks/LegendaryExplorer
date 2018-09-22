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
                case "WwiseStream":
                    Scan_WwiseStream(topLevelTree, data, binarystart);
                    break;
                case "WwiseEvent":
                    Scan_WwiseEvent(topLevelTree, data, binarystart);
                    break;
            }
        }

        private void Scan_WwiseStream(TreeViewItem topLevelTree, byte[] data, int binaryStart)
        {
            /*
             *  
             *  count +4
             *      stream length in AFC +4
             *      stream length in AFC +4 (repeat)
             *      stream offset in AFC +4
             *  
             */


            try
            {
                int pos = data.Length - 16;
                int unk1 = BitConverter.ToInt32(data, pos);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"{pos:X4} Unknown: {unk1}",
                    Name = "_" + pos.ToString(),
                });
                pos += 4;
                int length = BitConverter.ToInt32(data, pos);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"{pos:X4} stream length: {length} (0x{length:X})",
                    Name = "_" + pos.ToString(),
                    Tag = nodeType.StructLeafInt
                });
                pos += 4;
                length = BitConverter.ToInt32(data, pos);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"{ pos:X4} stream length: { length} (0x{ length:X})",
                    Name = "_" + pos.ToString(),
                    Tag = nodeType.StructLeafInt
                });
                pos += 4;
                int offset = BitConverter.ToInt32(data, pos);
                topLevelTree.Items.Add(new TreeViewItem()
                {
                    Header = $"{pos:X4} stream offset in file: {offset} (0x{offset:X})",
                    Name = "_" + pos.ToString(),
                    Tag = nodeType.StructLeafInt
                });
                pos += 4;
                if (pos < data.Length && CurrentLoadedExport.GetProperty<NameProperty>("Filename") == null)
                {
                    topLevelTree.Items.Add(new TreeViewItem()
                    {
                        Header = $"{pos:X4} Embedded sound data. Can be extracted with Soundplorer.",
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
                        Name = "_"+binarypos.ToString()
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
    }
}
